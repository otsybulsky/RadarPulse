namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingDirectHotReliefPlanner
{
    public RadarProcessingRebalanceDecision Plan(
        long decisionId,
        RadarProcessingPressureWindow pressureWindow,
        RadarProcessingRebalancePolicyState policyState)
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

        EnsureCompatibleShape(pressureWindow, policyState);

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
            return RadarProcessingRebalanceDecision.RejectedCandidate(
                decisionId,
                policyState.EvaluationSequence,
                pressureWindow.LatestTopologyVersion,
                pressureWindow.SampleCount,
                rejected.Candidate,
                [
                    RadarProcessingRebalanceSkippedReason.DirectHotPartitionHasNoSafeTarget,
                    rejected.TargetSkippedReason!.Value
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
                .OrderByDescending(partition => partition.AverageScore.Value)
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
                RadarProcessingRebalanceMoveKind.DirectHotRelief,
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
        RadarProcessingRebalancePolicyState policyState)
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
    }

    private sealed record CandidateEvaluation(
        RadarProcessingRebalanceCandidate Candidate,
        RadarProcessingPressureScore PartitionPressure,
        RadarProcessingRebalanceSkippedReason? TargetSkippedReason);
}
