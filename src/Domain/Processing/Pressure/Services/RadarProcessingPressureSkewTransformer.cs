namespace RadarPulse.Domain.Processing;

/// <summary>
/// Applies synthetic pressure skew profiles to pressure samples.
/// </summary>
/// <remarks>
/// Skew is used by local benchmarks and rebalance exercises to create pressure
/// shapes without changing processing telemetry generation. Disabled skew returns
/// the original sample unchanged.
/// </remarks>
public sealed class RadarProcessingPressureSkewTransformer
{
    /// <summary>
    /// Creates a pressure skew transformer.
    /// </summary>
    public RadarProcessingPressureSkewTransformer(
        RadarProcessingPressureSkewOptions? options = null)
    {
        Options = options ?? RadarProcessingPressureSkewOptions.None;
    }

    /// <summary>
    /// Skew options used by this transformer.
    /// </summary>
    public RadarProcessingPressureSkewOptions Options { get; }

    /// <summary>
    /// Applies the configured skew profile to a pressure sample.
    /// </summary>
    /// <returns>
    /// The original sample when skew is disabled; otherwise a new sample with adjusted scores.
    /// </returns>
    public RadarProcessingPressureSample Apply(
        RadarProcessingPressureSample sample,
        long evaluationSequence,
        RadarProcessingPressureWindowOptions? windowOptions = null)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(evaluationSequence);
        windowOptions ??= RadarProcessingPressureWindowOptions.Default;

        if (!Options.IsEnabled)
        {
            return sample;
        }

        return Options.Profile switch
        {
            RadarProcessingPressureSkewProfile.HotShard =>
                ApplyHotShard(sample, sourceShardId: 0, windowOptions, movableTarget: true),
            RadarProcessingPressureSkewProfile.RotatingHotShard =>
                ApplyHotShard(sample, SelectRotatingShard(sample.ShardCount, evaluationSequence), windowOptions, movableTarget: true),
            RadarProcessingPressureSkewProfile.BudgetStorm =>
                ApplyHotShard(sample, sourceShardId: 0, windowOptions, movableTarget: true),
            RadarProcessingPressureSkewProfile.HotPartition =>
                ApplyHotPartition(sample, sourceShardId: 0, windowOptions),
            RadarProcessingPressureSkewProfile.TargetStarvation =>
                ApplyTargetStarvation(sample, windowOptions),
            RadarProcessingPressureSkewProfile.None => sample,
            _ => throw new ArgumentOutOfRangeException(nameof(Options.Profile))
        };
    }

    private RadarProcessingPressureSample ApplyHotShard(
        RadarProcessingPressureSample sample,
        int sourceShardId,
        RadarProcessingPressureWindowOptions windowOptions,
        bool movableTarget)
    {
        if ((uint)sourceShardId >= (uint)sample.ShardCount)
        {
            sourceShardId = 0;
        }

        var partitionScores = new double[sample.PartitionCount];
        var sourcePartitionCount = CountPartitionsOnShard(sample, sourceShardId);
        if (sourcePartitionCount == 0)
        {
            return sample;
        }

        var pressure = movableTarget
            ? CalculateMovablePartitionPressure(sourcePartitionCount, windowOptions)
            : windowOptions.HotEnterThreshold * Math.Max(Options.Factor, 1.0);

        for (var partitionId = 0; partitionId < sample.PartitionCount; partitionId++)
        {
            var partition = sample.Partitions[partitionId];
            if (partition.ShardId == sourceShardId)
            {
                partitionScores[partitionId] = pressure;
            }
        }

        return CreateSkewedSample(sample, partitionScores, windowOptions);
    }

    private RadarProcessingPressureSample ApplyHotPartition(
        RadarProcessingPressureSample sample,
        int sourceShardId,
        RadarProcessingPressureWindowOptions windowOptions)
    {
        var partitionScores = new double[sample.PartitionCount];
        for (var partitionId = 0; partitionId < sample.PartitionCount; partitionId++)
        {
            var partition = sample.Partitions[partitionId];
            if (partition.ShardId != sourceShardId)
            {
                continue;
            }

            partitionScores[partitionId] = windowOptions.HotEnterThreshold * Math.Max(Options.Factor, 1.0);
            break;
        }

        return CreateSkewedSample(sample, partitionScores, windowOptions);
    }

    private RadarProcessingPressureSample ApplyTargetStarvation(
        RadarProcessingPressureSample sample,
        RadarProcessingPressureWindowOptions windowOptions)
    {
        var partitionScores = new double[sample.PartitionCount];
        for (var shardId = 0; shardId < sample.ShardCount; shardId++)
        {
            var partitionCount = CountPartitionsOnShard(sample, shardId);
            if (partitionCount == 0)
            {
                continue;
            }

            var pressure = CalculateMovablePartitionPressure(partitionCount, windowOptions);
            for (var partitionId = 0; partitionId < sample.PartitionCount; partitionId++)
            {
                if (sample.Partitions[partitionId].ShardId == shardId)
                {
                    partitionScores[partitionId] = pressure;
                }
            }
        }

        return CreateSkewedSample(sample, partitionScores, windowOptions);
    }

    private double CalculateMovablePartitionPressure(
        int sourcePartitionCount,
        RadarProcessingPressureWindowOptions windowOptions)
    {
        var requestedHotness = Math.Max(Options.Factor, 1.0);
        var pressureForHotSource = windowOptions.HotEnterThreshold * requestedHotness / sourcePartitionCount;
        var safeTargetPressure = windowOptions.WarmEnterThreshold * 0.9;
        return Math.Max(1.0, Math.Min(safeTargetPressure, pressureForHotSource));
    }

    private static RadarProcessingPressureSample CreateSkewedSample(
        RadarProcessingPressureSample sample,
        double[] partitionScores,
        RadarProcessingPressureWindowOptions windowOptions)
    {
        var partitions = new RadarProcessingPartitionPressureSample[sample.PartitionCount];
        var shardScores = new double[sample.ShardCount];

        for (var partitionId = 0; partitionId < partitions.Length; partitionId++)
        {
            var current = sample.Partitions[partitionId];
            var score = new RadarProcessingPressureScore(partitionScores[partitionId]);
            partitions[partitionId] = new RadarProcessingPartitionPressureSample(
                current.PartitionId,
                current.ShardId,
                current.Metrics,
                score,
                windowOptions.Classify(score, RadarProcessingPressureBand.Cold));
            shardScores[current.ShardId] += score.Value;
        }

        var shards = new RadarProcessingShardPressureSample[sample.ShardCount];
        for (var shardId = 0; shardId < shards.Length; shardId++)
        {
            var current = sample.Shards[shardId];
            var score = new RadarProcessingPressureScore(shardScores[shardId]);
            shards[shardId] = new RadarProcessingShardPressureSample(
                current.ShardId,
                current.PartitionCount,
                current.ActivePartitionCount,
                current.Metrics,
                score,
                windowOptions.Classify(score, RadarProcessingPressureBand.Cold));
        }

        return RadarProcessingPressureSample.Create(
            sample.TopologyVersion,
            sample.BatchMetrics,
            shards,
            partitions);
    }

    private int SelectRotatingShard(
        int shardCount,
        long evaluationSequence) =>
        shardCount == 0
            ? 0
            : (int)(((evaluationSequence - 1) / Options.Period) % shardCount);

    private static int CountPartitionsOnShard(
        RadarProcessingPressureSample sample,
        int shardId)
    {
        var count = 0;
        foreach (var partition in sample.Partitions)
        {
            if (partition.ShardId == shardId)
            {
                count++;
            }
        }

        return count;
    }
}
