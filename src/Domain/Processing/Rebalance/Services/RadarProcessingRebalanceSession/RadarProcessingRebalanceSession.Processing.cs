using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public sealed partial class RadarProcessingRebalanceSession
{
    /// <summary>
    /// Processes a batch through the core and evaluates rebalance after processing completes.
    /// </summary>
    /// <returns>
    /// Session result containing processing output, pressure sample, decisions, migration,
    /// handoff validation, telemetry summary, and contract validation.
    /// </returns>
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

    /// <summary>
    /// Commits a worker processing delta and evaluates rebalance after the delta is applied.
    /// </summary>
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

    /// <summary>
    /// Processes a completed processing result through rebalance policy and telemetry.
    /// </summary>
    public RadarProcessingRebalanceSessionResult ProcessCompletedResult(
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
}
