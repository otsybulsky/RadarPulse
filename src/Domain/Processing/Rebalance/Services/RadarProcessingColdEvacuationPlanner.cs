namespace RadarPulse.Domain.Processing;

/// <summary>
/// Planner that evacuates a colder partition from a hot shard to create relief.
/// </summary>
/// <remarks>
/// Cold evacuation is a fallback after direct hot-relief fails. It selects lower
/// pressure non-hot partitions from hot shards, preserving target safety and then
/// applying the same policy state as other rebalance candidates.
/// </remarks>
public sealed class RadarProcessingColdEvacuationPlanner
{
    /// <summary>
    /// Plans one cold-evacuation rebalance decision for the current pressure window.
    /// </summary>
    /// <returns>
    /// No-action when pressure is not eligible or no evacuation candidate exists;
    /// otherwise an accepted or rejected candidate decision.
    /// </returns>
    public RadarProcessingRebalanceDecision Plan(
        long decisionId,
        RadarProcessingPressureWindow pressureWindow,
        RadarProcessingRebalancePolicyState policyState,
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

        EnsureCompatibleShape(pressureWindow, policyState, quarantineLifecycleTracker);

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

        var candidates = CreateCandidateEvaluations(pressureWindow, hotShards, coldTargets);
        if (candidates.Count == 0)
        {
            return NoAction(
                decisionId,
                pressureWindow,
                policyState,
                [RadarProcessingRebalanceSkippedReason.ColdEvacuationInsufficientBenefit]);
        }

        var selected = candidates
            .Where(candidate => candidate.TargetSkippedReason is null)
            .Where(candidate => candidate.Candidate.ExpectedRelief > 0.0)
            .OrderBy(candidate => candidate.Candidate.ExpectedRelief)
            .ThenBy(candidate => candidate.PartitionPressure.Value)
            .ThenBy(candidate => candidate.Candidate.PartitionId)
            .ThenBy(candidate => candidate.Candidate.TargetShardId)
            .FirstOrDefault();

        if (selected is null)
        {
            var rejected = candidates
                .OrderBy(candidate => candidate.Candidate.ExpectedRelief)
                .ThenBy(candidate => candidate.PartitionPressure.Value)
                .ThenBy(candidate => candidate.Candidate.PartitionId)
                .ThenBy(candidate => candidate.Candidate.TargetShardId)
                .First();

            return RadarProcessingRebalanceDecision.RejectedCandidate(
                decisionId,
                policyState.EvaluationSequence,
                pressureWindow.LatestTopologyVersion,
                pressureWindow.SampleCount,
                rejected.Candidate,
                rejected.TargetSkippedReason is null
                    ? [RadarProcessingRebalanceSkippedReason.ColdEvacuationInsufficientBenefit]
                    : [rejected.TargetSkippedReason.Value]);
        }

        var policyResult = policyState.EvaluateMove(selected.Candidate.ToPolicyInput());
        if (policyResult.IsAllowed)
        {
            return RadarProcessingRebalanceDecision.AcceptedMove(
                decisionId,
                policyState.EvaluationSequence,
                pressureWindow.LatestTopologyVersion,
                pressureWindow.SampleCount,
                selected.Candidate);
        }

        if (policyResult.Rejections.Contains(RadarProcessingRebalancePolicyRejection.InsufficientProjectedBenefit))
        {
            return RadarProcessingRebalanceDecision.RejectedCandidate(
                decisionId,
                policyState.EvaluationSequence,
                pressureWindow.LatestTopologyVersion,
                pressureWindow.SampleCount,
                selected.Candidate,
                [
                    RadarProcessingRebalanceSkippedReason.ColdEvacuationInsufficientBenefit,
                    RadarProcessingRebalanceSkippedReason.InsufficientProjectedBenefit
                ],
                policyResult.Rejections);
        }

        return RadarProcessingRebalanceDecision.RejectedCandidate(
            decisionId,
            policyState.EvaluationSequence,
            pressureWindow.LatestTopologyVersion,
            pressureWindow.SampleCount,
            selected.Candidate,
            policyResult);
    }

    private static List<CandidateEvaluation> CreateCandidateEvaluations(
        RadarProcessingPressureWindow pressureWindow,
        IReadOnlyCollection<RadarProcessingShardPressureState> hotShards,
        IReadOnlyCollection<RadarProcessingShardPressureState> coldTargets)
    {
        var result = new List<CandidateEvaluation>();

        foreach (var sourceShard in hotShards)
        {
            var sourcePartitions = pressureWindow.Partitions
                .Where(partition => partition.ShardId == sourceShard.ShardId)
                .Where(partition => partition.AverageScore.Value > 0.0)
                .Where(partition => !partition.IsHot)
                .OrderBy(partition => partition.AverageScore.Value)
                .ThenBy(partition => partition.PartitionId);

            foreach (var partition in sourcePartitions)
            {
                foreach (var targetShard in coldTargets)
                {
                    if (targetShard.ShardId == sourceShard.ShardId)
                    {
                        continue;
                    }

                    result.Add(CreateCandidateEvaluation(pressureWindow, sourceShard, partition, targetShard));
                }
            }
        }

        return result;
    }

    private static CandidateEvaluation CreateCandidateEvaluation(
        RadarProcessingPressureWindow pressureWindow,
        RadarProcessingShardPressureState sourceShard,
        RadarProcessingPartitionPressureState partition,
        RadarProcessingShardPressureState targetShard)
    {
        var partitionPressure = partition.AverageScore.Value;
        var projectedSource = new RadarProcessingPressureScore(
            Math.Max(0.0, sourceShard.AverageScore.Value - partitionPressure));
        var projectedTarget = new RadarProcessingPressureScore(
            targetShard.AverageScore.Value + partitionPressure);
        var projectedMaxPressure = Math.Max(projectedSource.Value, projectedTarget.Value);
        var expectedRelief = Math.Max(0.0, sourceShard.AverageScore.Value - projectedMaxPressure);
        var projectedTargetBand = pressureWindow.Options.Classify(projectedTarget, targetShard.Band);
        var targetSkippedReason = projectedTargetBand switch
        {
            RadarProcessingPressureBand.Warm => RadarProcessingRebalanceSkippedReason.TargetWouldBecomeWarm,
            RadarProcessingPressureBand.Hot or RadarProcessingPressureBand.SuperHot =>
                RadarProcessingRebalanceSkippedReason.TargetWouldBecomeHot,
            _ => (RadarProcessingRebalanceSkippedReason?)null
        };

        return new CandidateEvaluation(
            new RadarProcessingRebalanceCandidate(
                RadarProcessingRebalanceMoveKind.ColdEvacuation,
                partition.PartitionId,
                sourceShard.ShardId,
                targetShard.ShardId,
                new RadarProcessingProjectedPressure(
                    sourceShard.AverageScore,
                    targetShard.AverageScore,
                    projectedSource,
                    projectedTarget),
                expectedRelief),
            partition.AverageScore,
            targetSkippedReason);
    }

    private static RadarProcessingRebalanceDecision NoAction(
        long decisionId,
        RadarProcessingPressureWindow pressureWindow,
        RadarProcessingRebalancePolicyState policyState,
        IReadOnlyCollection<RadarProcessingRebalanceSkippedReason> skippedReasons) =>
        RadarProcessingRebalanceDecision.NoAction(
            decisionId,
            policyState.EvaluationSequence,
            pressureWindow.LatestTopologyVersion,
            pressureWindow.SampleCount,
            skippedReasons);

    private static void EnsureCompatibleShape(
        RadarProcessingPressureWindow pressureWindow,
        RadarProcessingRebalancePolicyState policyState,
        RadarProcessingQuarantineLifecycleTracker? quarantineLifecycleTracker)
    {
        if (pressureWindow.Partitions.Count != policyState.PartitionCount)
        {
            throw new ArgumentException(
                "Pressure window partition count must match rebalance policy state.",
                nameof(pressureWindow));
        }

        if (pressureWindow.Shards.Count != policyState.ShardCount)
        {
            throw new ArgumentException(
                "Pressure window shard count must match rebalance policy state.",
                nameof(pressureWindow));
        }

        if (quarantineLifecycleTracker is not null &&
            pressureWindow.Partitions.Count != quarantineLifecycleTracker.PartitionCount)
        {
            throw new ArgumentException(
                "Pressure window partition count must match quarantine lifecycle state.",
                nameof(pressureWindow));
        }
    }

    private sealed record CandidateEvaluation(
        RadarProcessingRebalanceCandidate Candidate,
        RadarProcessingPressureScore PartitionPressure,
        RadarProcessingRebalanceSkippedReason? TargetSkippedReason);
}
