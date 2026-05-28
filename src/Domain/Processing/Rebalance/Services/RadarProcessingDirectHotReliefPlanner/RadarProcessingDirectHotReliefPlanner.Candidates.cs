namespace RadarPulse.Domain.Processing;

public sealed partial class RadarProcessingDirectHotReliefPlanner
{
    private static List<CandidateEvaluation> CreateCandidateEvaluations(
        RadarProcessingPressureWindow pressureWindow,
        IReadOnlyCollection<RadarProcessingShardPressureState> hotShards,
        IReadOnlyCollection<RadarProcessingShardPressureState> coldTargets,
        RadarProcessingHotPartitionClassifier? hotPartitionClassifier,
        RadarProcessingQuarantineLifecycleTracker? quarantineLifecycleTracker,
        List<RadarProcessingRebalanceSkippedReason> skippedClassificationReasons)
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
                if ((hotPartitionClassifier is not null || quarantineLifecycleTracker is not null) &&
                    TrySkipClassifiedPartition(
                        hotPartitionClassifier,
                        quarantineLifecycleTracker,
                        partition.PartitionId,
                        skippedClassificationReasons))
                {
                    continue;
                }

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
            partition,
            partition.AverageScore,
            targetSkippedReason);
    }
}
