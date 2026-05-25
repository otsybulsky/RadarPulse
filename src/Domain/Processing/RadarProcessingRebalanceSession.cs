using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingRebalanceSession
{
    private readonly RadarProcessingCore core;
    private readonly RadarProcessingPressureOptions pressureOptions;
    private readonly RadarProcessingPressureWindow pressureWindow;
    private readonly RadarProcessingRebalancePolicyState policyState;
    private readonly RadarProcessingHotPartitionClassifier hotPartitionClassifier;
    private readonly RadarProcessingQuarantineLifecycleTracker quarantineLifecycleTracker;
    private readonly RadarProcessingRebalanceTelemetryRecorder telemetryRecorder;
    private readonly RadarProcessingRebalanceHardeningOptions hardeningOptions;
    private readonly RadarProcessingPressureSkewTransformer? pressureSkewTransformer;
    private readonly RadarProcessingDirectHotReliefPlanner directHotReliefPlanner;
    private readonly RadarProcessingColdEvacuationPlanner coldEvacuationPlanner;
    private readonly RadarProcessingMigrationCoordinator migrationCoordinator;
    private long nextDecisionId = 1;

    public RadarProcessingRebalanceSession(
        RadarProcessingCore core,
        RadarProcessingPressureOptions? pressureOptions = null,
        RadarProcessingPressureWindow? pressureWindow = null,
        RadarProcessingRebalancePolicyState? policyState = null,
        RadarProcessingHotPartitionClassifier? hotPartitionClassifier = null,
        RadarProcessingDirectHotReliefPlanner? directHotReliefPlanner = null,
        RadarProcessingColdEvacuationPlanner? coldEvacuationPlanner = null,
        RadarProcessingQuarantineLifecycleTracker? quarantineLifecycleTracker = null,
        RadarProcessingRebalanceTelemetryRecorder? telemetryRecorder = null,
        RadarProcessingRebalanceHardeningOptions? hardeningOptions = null,
        RadarProcessingPressureSkewOptions? pressureSkewOptions = null)
    {
        ArgumentNullException.ThrowIfNull(core);

        if (core.Options.ExecutionMode is not RadarProcessingExecutionMode.PartitionedBarrier and
            not RadarProcessingExecutionMode.AsyncShardTransport)
        {
            throw new ArgumentException(
                "Rebalance sessions require partitioned barrier or async shard transport processing.",
                nameof(core));
        }

        this.core = core;
        this.hardeningOptions = hardeningOptions ?? RadarProcessingRebalanceHardeningOptions.Default;
        pressureSkewTransformer = pressureSkewOptions?.IsEnabled == true
            ? new RadarProcessingPressureSkewTransformer(pressureSkewOptions)
            : null;
        this.pressureOptions = pressureOptions ?? RadarProcessingPressureOptions.Default;
        this.pressureWindow = pressureWindow ?? new RadarProcessingPressureWindow();
        this.policyState = policyState ?? new RadarProcessingRebalancePolicyState(
            core.Options.PartitionCount,
            core.Options.ShardCount);
        this.hotPartitionClassifier = hotPartitionClassifier ??
                                      new RadarProcessingHotPartitionClassifier(core.Options.PartitionCount);
        this.quarantineLifecycleTracker = quarantineLifecycleTracker ??
                                          new RadarProcessingQuarantineLifecycleTracker(
                                              core.Options.PartitionCount,
                                              this.hardeningOptions.QuarantineLifecycle);
        this.telemetryRecorder = telemetryRecorder ??
                                 new RadarProcessingRebalanceTelemetryRecorder(this.hardeningOptions.TelemetryRetention);
        this.directHotReliefPlanner = directHotReliefPlanner ?? new RadarProcessingDirectHotReliefPlanner();
        this.coldEvacuationPlanner = coldEvacuationPlanner ?? new RadarProcessingColdEvacuationPlanner();
        migrationCoordinator = new RadarProcessingMigrationCoordinator(core.TopologyManager);

        EnsureCompatibleShape(this.policyState, this.hotPartitionClassifier, this.quarantineLifecycleTracker);
    }

    public RadarProcessingCore Core => core;

    public RadarProcessingTopology CurrentTopology => core.Topology;

    public RadarProcessingPressureWindow PressureWindow => pressureWindow;

    public RadarProcessingRebalancePolicyState PolicyState => policyState;

    public RadarProcessingHotPartitionClassifier HotPartitionClassifier => hotPartitionClassifier;

    public RadarProcessingQuarantineLifecycleTracker QuarantineLifecycleTracker => quarantineLifecycleTracker;

    public RadarProcessingRebalanceTelemetryRecorder TelemetryRecorder => telemetryRecorder;

    public RadarProcessingRebalanceHardeningOptions HardeningOptions => hardeningOptions;

    public RadarProcessingValidationProfile ValidationProfile => hardeningOptions.ValidationProfile;

    public RadarProcessingRebalanceSessionResult Process(
        RadarEventBatch batch,
        CancellationToken cancellationToken = default)
    {
        if (core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            throw new NotSupportedException(
                "Async shard transport rebalance execution requires RadarProcessingAsyncRebalanceSession.ProcessAsync.");
        }

        var processingResult = core.Process(batch, cancellationToken);
        return ProcessCompletedResult(processingResult, cancellationToken);
    }

    public RadarProcessingRebalanceSessionResult CommitProcessingDelta(
        RadarProcessingBatchDelta delta,
        RadarProcessingWorkerTelemetrySummary? workerTelemetry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delta);

        var processingResult = core.CommitProcessingDelta(
            delta,
            workerTelemetry,
            cancellationToken);
        return ProcessCompletedResult(processingResult, cancellationToken);
    }

    internal RadarProcessingRebalanceSessionResult ProcessCompletedResult(
        RadarProcessingResult processingResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processingResult);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureCompatibleProcessingResult(processingResult);

        quarantineLifecycleTracker.DrainTransitions();
        if (!processingResult.IsValid || processingResult.Telemetry is null)
        {
            var validation = ValidateSessionResult(
                processingResult,
                pressureSample: null,
                directHotReliefDecision: null,
                coldEvacuationDecision: null,
                migrationResult: null,
                handoffValidation: null);
            RecordValidationResult(processingResult, validation);

            return new RadarProcessingRebalanceSessionResult(
                processingResult,
                pressureSample: null,
                directHotReliefDecision: null,
                coldEvacuationDecision: null,
                migrationResult: null,
                handoffValidation: null,
                currentTopology: core.Topology,
                quarantineTransitions: Array.Empty<RadarProcessingQuarantineTransition>(),
                telemetrySummary: telemetryRecorder.CreateSummary(),
                validationProfile: ValidationProfile,
                validation: validation);
        }

        var pressureSample = RadarProcessingPressureSample.FromTelemetry(
            processingResult.Telemetry,
            pressureOptions);
        var effectivePressureSample = pressureSkewTransformer?.Apply(
            pressureSample,
            policyState.EvaluationSequence + 1,
            pressureWindow.Options) ?? pressureSample;
        pressureWindow.AddSample(effectivePressureSample);
        policyState.AdvanceEvaluation();
        AdvanceQuarantineLifecycleBeforePlanning();

        var directDecision = directHotReliefPlanner.Plan(
            NextDecisionId(),
            pressureWindow,
            policyState,
            hotPartitionClassifier,
            quarantineLifecycleTracker);
        RadarProcessingRebalanceDecision? coldDecision = null;
        var selectedDecision = directDecision;

        if (!directDecision.HasAcceptedMove && pressureWindow.IsRebalanceEligible)
        {
            coldDecision = coldEvacuationPlanner.Plan(
                NextDecisionId(),
                pressureWindow,
                policyState,
                quarantineLifecycleTracker);
            selectedDecision = coldDecision;
        }

        telemetryRecorder.RecordDecision(directDecision);
        if (coldDecision is not null)
        {
            telemetryRecorder.RecordDecision(coldDecision);
        }

        var (migrationResult, handoffValidation) = selectedDecision.HasAcceptedMove
            ? ApplyAcceptedMove(selectedDecision)
            : (null, null);
        var quarantineTransitions = quarantineLifecycleTracker.DrainTransitions();
        RecordQuarantineTransitions(quarantineTransitions);
        var sessionValidation = ValidateSessionResult(
            processingResult,
            pressureSample,
            directDecision,
            coldDecision,
            migrationResult,
            handoffValidation);
        RecordValidationResult(processingResult, sessionValidation);

        return new RadarProcessingRebalanceSessionResult(
            processingResult,
            pressureSample,
            directDecision,
            coldDecision,
            migrationResult,
            handoffValidation,
            core.Topology,
            quarantineTransitions,
            telemetryRecorder.CreateSummary(),
            ValidationProfile,
            sessionValidation);
    }

    private RadarProcessingRebalanceValidationResult ValidateSessionResult(
        RadarProcessingResult processingResult,
        RadarProcessingPressureSample? pressureSample,
        RadarProcessingRebalanceDecision? directHotReliefDecision,
        RadarProcessingRebalanceDecision? coldEvacuationDecision,
        RadarProcessingMigrationResult? migrationResult,
        RadarProcessingStateHandoffValidationResult? handoffValidation) =>
        RadarProcessingRebalanceValidator.ValidateSessionResult(
            processingResult,
            pressureSample,
            directHotReliefDecision,
            coldEvacuationDecision,
            migrationResult,
            handoffValidation,
            core.Topology,
            ValidationProfile);

    private void RecordValidationResult(
        RadarProcessingResult processingResult,
        RadarProcessingRebalanceValidationResult validation)
    {
        telemetryRecorder.RecordValidationResult(
            policyState.EvaluationSequence,
            processingResult.TopologyVersion,
            validation);
    }

    private void RecordQuarantineTransitions(
        IReadOnlyList<RadarProcessingQuarantineTransition> transitions)
    {
        foreach (var transition in transitions)
        {
            telemetryRecorder.RecordQuarantineTransition(transition);
        }
    }

    private void AdvanceQuarantineLifecycleBeforePlanning()
    {
        foreach (var partition in pressureWindow.Partitions)
        {
            quarantineLifecycleTracker.RecordPartitionEvidence(
                partition,
                policyState.EvaluationSequence,
                pressureWindow.LatestTopologyVersion,
                GetObservedClassificationForLifecycle(partition.PartitionId));
        }
    }

    private RadarProcessingHotPartitionClassification GetObservedClassificationForLifecycle(
        int partitionId)
    {
        var lifecycleState = quarantineLifecycleTracker.GetPartition(partitionId);
        if (lifecycleState.HasQuarantineEvidence)
        {
            return RadarProcessingHotPartitionClassification.None;
        }

        return hotPartitionClassifier.GetPartition(partitionId).Classification;
    }

    private (RadarProcessingMigrationResult? Migration, RadarProcessingStateHandoffValidationResult? Handoff)
        ApplyAcceptedMove(RadarProcessingRebalanceDecision decision)
    {
        var candidate = decision.Candidate ??
                        throw new InvalidOperationException("Accepted rebalance decisions must include a candidate.");
        var current = core.Topology;
        var beforePartition = current.GetPartition(candidate.PartitionId);
        var beforeSnapshot = core.CapturePartitionState(beforePartition);
        var projectedAfterSnapshot = core.CapturePartitionState(
            new RadarProcessingPartitionAssignment(
                beforePartition.PartitionId,
                candidate.TargetShardId,
                beforePartition.SourceIdStart,
                beforePartition.SourceIdEndExclusive));
        var prePublicationHandoff = RadarProcessingStateHandoffValidator.Validate(
            beforeSnapshot,
            projectedAfterSnapshot);

        if (!prePublicationHandoff.IsValid)
        {
            return (null, prePublicationHandoff);
        }

        var migrationResult = migrationCoordinator.Apply(decision);
        if (!migrationResult.Succeeded)
        {
            return (migrationResult, prePublicationHandoff);
        }

        var afterSnapshot = core.CapturePartitionState(core.Topology.GetPartition(candidate.PartitionId));
        var handoff = RadarProcessingStateHandoffValidator.Validate(
            beforeSnapshot,
            afterSnapshot);

        if (handoff.IsValid)
        {
            var policyRecord = policyState.RecordAcceptedMove(candidate.ToPolicyInput());
            if (!policyRecord.IsAllowed)
            {
                throw new InvalidOperationException("Published rebalance move failed policy recording.");
            }
        }

        return (migrationResult, handoff);
    }

    private long NextDecisionId() =>
        nextDecisionId++;

    private void EnsureCompatibleProcessingResult(
        RadarProcessingResult processingResult)
    {
        if (processingResult.ExecutionMode != core.Options.ExecutionMode)
        {
            throw new ArgumentException(
                "Processing result execution mode must match the rebalance session core.",
                nameof(processingResult));
        }

        if (processingResult.PartitionCount != core.Options.PartitionCount ||
            processingResult.ShardCount != core.Options.ShardCount)
        {
            throw new ArgumentException(
                "Processing result topology shape must match the rebalance session core.",
                nameof(processingResult));
        }

        if (processingResult.TopologyVersion != core.Topology.Version)
        {
            throw new ArgumentException(
                "Processing result topology version must match the current rebalance session topology.",
                nameof(processingResult));
        }
    }

    private void EnsureCompatibleShape(
        RadarProcessingRebalancePolicyState candidatePolicyState,
        RadarProcessingHotPartitionClassifier candidateHotPartitionClassifier,
        RadarProcessingQuarantineLifecycleTracker candidateQuarantineLifecycleTracker)
    {
        if (candidatePolicyState.PartitionCount != core.Options.PartitionCount)
        {
            throw new ArgumentException(
                "Rebalance policy partition count must match the processing core.",
                nameof(policyState));
        }

        if (candidatePolicyState.ShardCount != core.Options.ShardCount)
        {
            throw new ArgumentException(
                "Rebalance policy shard count must match the processing core.",
                nameof(policyState));
        }

        if (candidateHotPartitionClassifier.PartitionCount != core.Options.PartitionCount)
        {
            throw new ArgumentException(
                "Hot partition classifier partition count must match the processing core.",
                nameof(hotPartitionClassifier));
        }

        if (candidateQuarantineLifecycleTracker.PartitionCount != core.Options.PartitionCount)
        {
            throw new ArgumentException(
                "Quarantine lifecycle partition count must match the processing core.",
                nameof(quarantineLifecycleTracker));
        }
    }
}
