namespace RadarPulse.Domain.Processing;

public sealed partial class RadarProcessingDirectHotReliefPlanner
{
    private static void RecordMovableDirectCandidate(
        RadarProcessingPressureWindow pressureWindow,
        RadarProcessingRebalancePolicyState policyState,
        RadarProcessingHotPartitionClassifier? hotPartitionClassifier,
        RadarProcessingQuarantineLifecycleTracker? quarantineLifecycleTracker,
        CandidateEvaluation selected)
    {
        hotPartitionClassifier?.ClassifyMovableHot(
            selected.Candidate.PartitionId,
            selected.Candidate.SourceShardId,
            policyState.EvaluationSequence);
        quarantineLifecycleTracker?.RecordPartitionEvidence(
            selected.Partition,
            policyState.EvaluationSequence,
            pressureWindow.LatestTopologyVersion,
            RadarProcessingHotPartitionClassification.MovableHot);
    }

    private static RadarProcessingRebalanceSkippedReason RecordRejectedDirectCandidate(
        RadarProcessingPressureWindow pressureWindow,
        RadarProcessingRebalancePolicyState policyState,
        RadarProcessingHotPartitionClassifier? hotPartitionClassifier,
        RadarProcessingQuarantineLifecycleTracker? quarantineLifecycleTracker,
        CandidateEvaluation rejected)
    {
        var lifecycleState = quarantineLifecycleTracker?.GetPartition(rejected.Candidate.PartitionId);
        var observedClassification = lifecycleState?.IsRetryEligible == true
            ? RadarProcessingHotPartitionClassification.Quarantined
            : RadarProcessingHotPartitionClassification.IntrinsicHot;

        if (observedClassification == RadarProcessingHotPartitionClassification.Quarantined)
        {
            hotPartitionClassifier?.ClassifyQuarantined(
                rejected.Candidate.PartitionId,
                rejected.Candidate.SourceShardId,
                policyState.EvaluationSequence);
        }
        else
        {
            hotPartitionClassifier?.ClassifyIntrinsicHot(
                rejected.Candidate.PartitionId,
                rejected.Candidate.SourceShardId,
                policyState.EvaluationSequence);
        }

        quarantineLifecycleTracker?.RecordPartitionEvidence(
            rejected.Partition,
            policyState.EvaluationSequence,
            pressureWindow.LatestTopologyVersion,
            observedClassification);

        return observedClassification == RadarProcessingHotPartitionClassification.Quarantined
            ? RadarProcessingRebalanceSkippedReason.PartitionQuarantined
            : RadarProcessingRebalanceSkippedReason.PartitionClassifiedIntrinsicHot;
    }
    private sealed record CandidateEvaluation(
        RadarProcessingRebalanceCandidate Candidate,
        RadarProcessingPartitionPressureState Partition,
        RadarProcessingPressureScore PartitionPressure,
        RadarProcessingRebalanceSkippedReason? TargetSkippedReason);
}
