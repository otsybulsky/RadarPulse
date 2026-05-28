namespace RadarPulse.Domain.Processing;

/// <summary>
/// Planner that tries to move a hot partition from a hot shard to a cold shard.
/// </summary>
/// <remarks>
/// The planner prioritizes the hottest shards and partitions, rejects targets
/// that would become warm or hot, and consults optional hot-partition/quarantine
/// state before returning an accepted or rejected candidate.
public sealed partial class RadarProcessingDirectHotReliefPlanner
{
    /// <summary>
    /// Plans one direct hot-relief rebalance decision for the current pressure window.
    /// </summary>
    /// <returns>
    /// No-action when pressure is not eligible or no safe candidate exists; otherwise
    /// an accepted or rejected candidate decision with explicit reasons.
    /// </returns>
    public RadarProcessingRebalanceDecision Plan(
        long decisionId,
        RadarProcessingPressureWindow pressureWindow,
        RadarProcessingRebalancePolicyState policyState,
        RadarProcessingHotPartitionClassifier? hotPartitionClassifier = null,
        RadarProcessingQuarantineLifecycleTracker? quarantineLifecycleTracker = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(decisionId);
        ArgumentNullException.ThrowIfNull(pressureWindow);
        ArgumentNullException.ThrowIfNull(policyState);

        if (!pressureWindow.IsRebalanceEligible)
        {
            return NoAction(
                decisionId,
                pressureWindow,
                policyState,
                [RadarProcessingRebalanceSkippedReason.NoSustainedPressure]);
        }

        EnsureCompatibleShape(pressureWindow, policyState, hotPartitionClassifier, quarantineLifecycleTracker);

        var hotShards = pressureWindow.Shards
            .Where(shard => shard.IsHot)
            .OrderByDescending(shard => shard.AverageScore.Value)
            .ThenBy(shard => shard.ShardId)
            .ToArray();
        if (hotShards.Length == 0)
        {
            return NoAction(
                decisionId,
                pressureWindow,
                policyState,
                [RadarProcessingRebalanceSkippedReason.NoHotShard]);
        }

        var coldTargets = pressureWindow.Shards
            .Where(shard => shard.Band == RadarProcessingPressureBand.Cold)
            .OrderBy(shard => shard.AverageScore.Value)
            .ThenBy(shard => shard.ShardId)
            .ToArray();
        if (coldTargets.Length == 0)
        {
            return NoAction(
                decisionId,
                pressureWindow,
                policyState,
                [RadarProcessingRebalanceSkippedReason.NoColdTargetShard]);
        }

        var skippedClassificationReasons = new List<RadarProcessingRebalanceSkippedReason>();
        var candidates = CreateCandidateEvaluations(
            pressureWindow,
            hotShards,
            coldTargets,
            hotPartitionClassifier,
            quarantineLifecycleTracker,
            skippedClassificationReasons);
        if (candidates.Count == 0)
        {
            if (skippedClassificationReasons.Count > 0)
            {
                return NoAction(
                    decisionId,
                    pressureWindow,
                    policyState,
                    skippedClassificationReasons);
            }

            return NoAction(
                decisionId,
                pressureWindow,
                policyState,
                [RadarProcessingRebalanceSkippedReason.DirectHotPartitionHasNoSafeTarget]);
        }

        var selected = candidates
            .Where(candidate => candidate.TargetSkippedReason is null)
            .OrderByDescending(candidate => candidate.Candidate.ExpectedRelief)
            .ThenByDescending(candidate => candidate.PartitionPressure.Value)
            .ThenBy(candidate => candidate.Candidate.PartitionId)
            .ThenBy(candidate => candidate.Candidate.TargetShardId)
            .FirstOrDefault();

        if (selected is null)
        {
            var rejected = candidates
                .OrderByDescending(candidate => candidate.Candidate.ExpectedRelief)
                .ThenByDescending(candidate => candidate.PartitionPressure.Value)
                .ThenBy(candidate => candidate.Candidate.PartitionId)
                .ThenBy(candidate => candidate.Candidate.TargetShardId)
                .First();
            var classificationReason = RecordRejectedDirectCandidate(
                pressureWindow,
                policyState,
                hotPartitionClassifier,
                quarantineLifecycleTracker,
                rejected);

            return RadarProcessingRebalanceDecision.RejectedCandidate(
                decisionId,
                policyState.EvaluationSequence,
                pressureWindow.LatestTopologyVersion,
                pressureWindow.SampleCount,
                rejected.Candidate,
                [
                    RadarProcessingRebalanceSkippedReason.DirectHotPartitionHasNoSafeTarget,
                    rejected.TargetSkippedReason!.Value,
                    classificationReason
                ]);
        }

        if (selected.Candidate.ExpectedRelief <= 0.0)
        {
            return RadarProcessingRebalanceDecision.RejectedCandidate(
                decisionId,
                policyState.EvaluationSequence,
                pressureWindow.LatestTopologyVersion,
                pressureWindow.SampleCount,
                selected.Candidate,
                [RadarProcessingRebalanceSkippedReason.InsufficientProjectedBenefit]);
        }

        RecordMovableDirectCandidate(
            pressureWindow,
            policyState,
            hotPartitionClassifier,
            quarantineLifecycleTracker,
            selected);

        var policyResult = policyState.EvaluateMove(selected.Candidate.ToPolicyInput());
        return policyResult.IsAllowed
            ? RadarProcessingRebalanceDecision.AcceptedMove(
                decisionId,
                policyState.EvaluationSequence,
                pressureWindow.LatestTopologyVersion,
                pressureWindow.SampleCount,
                selected.Candidate)
            : RadarProcessingRebalanceDecision.RejectedCandidate(
                decisionId,
                policyState.EvaluationSequence,
                pressureWindow.LatestTopologyVersion,
                pressureWindow.SampleCount,
                selected.Candidate,
                policyResult);
    }
}
