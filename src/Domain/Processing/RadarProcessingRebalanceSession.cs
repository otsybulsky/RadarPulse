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
        RadarProcessingQuarantineLifecycleTracker? quarantineLifecycleTracker = null)
    {
        ArgumentNullException.ThrowIfNull(core);

        if (core.Options.ExecutionMode != RadarProcessingExecutionMode.PartitionedBarrier)
        {
            throw new ArgumentException(
                "Rebalance sessions require partitioned barrier processing.",
                nameof(core));
        }

        this.core = core;
        this.pressureOptions = pressureOptions ?? RadarProcessingPressureOptions.Default;
        this.pressureWindow = pressureWindow ?? new RadarProcessingPressureWindow();
        this.policyState = policyState ?? new RadarProcessingRebalancePolicyState(
            core.Options.PartitionCount,
            core.Options.ShardCount);
        this.hotPartitionClassifier = hotPartitionClassifier ??
                                      new RadarProcessingHotPartitionClassifier(core.Options.PartitionCount);
        this.quarantineLifecycleTracker = quarantineLifecycleTracker ??
                                          new RadarProcessingQuarantineLifecycleTracker(core.Options.PartitionCount);
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

    public RadarProcessingRebalanceSessionResult Process(
        RadarEventBatch batch,
        CancellationToken cancellationToken = default)
    {
        quarantineLifecycleTracker.DrainTransitions();

        var processingResult = core.Process(batch, cancellationToken);
        if (!processingResult.IsValid || processingResult.Telemetry is null)
        {
            return new RadarProcessingRebalanceSessionResult(
                processingResult,
                pressureSample: null,
                directHotReliefDecision: null,
                coldEvacuationDecision: null,
                migrationResult: null,
                handoffValidation: null,
                currentTopology: core.Topology,
                quarantineTransitions: Array.Empty<RadarProcessingQuarantineTransition>());
        }

        var pressureSample = RadarProcessingPressureSample.FromTelemetry(
            processingResult.Telemetry,
            pressureOptions);
        pressureWindow.AddSample(pressureSample);
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

        var (migrationResult, handoffValidation) = selectedDecision.HasAcceptedMove
            ? ApplyAcceptedMove(selectedDecision)
            : (null, null);

        return new RadarProcessingRebalanceSessionResult(
            processingResult,
            pressureSample,
            directDecision,
            coldDecision,
            migrationResult,
            handoffValidation,
            core.Topology,
            quarantineLifecycleTracker.DrainTransitions());
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
