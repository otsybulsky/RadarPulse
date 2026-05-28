namespace RadarPulse.Domain.Processing;

public sealed partial class RadarProcessingRebalanceSession
{
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
}
