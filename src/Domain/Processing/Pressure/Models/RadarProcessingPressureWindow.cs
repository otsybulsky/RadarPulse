namespace RadarPulse.Domain.Processing;

/// <summary>
/// Rolling pressure window used to decide whether rebalance planning is eligible.
/// </summary>
/// <remarks>
/// The window retains a bounded number of compatible samples, rebuilds rolling
/// shard and partition state after each sample, and uses hysteresis thresholds to
/// avoid pressure band churn.
/// </remarks>
public sealed class RadarProcessingPressureWindow
{
    private readonly Queue<RadarProcessingPressureSample> samples = new();
    private IReadOnlyList<RadarProcessingShardPressureState> shards =
        Array.Empty<RadarProcessingShardPressureState>();
    private IReadOnlyList<RadarProcessingPartitionPressureState> partitions =
        Array.Empty<RadarProcessingPartitionPressureState>();

    /// <summary>
    /// Creates a pressure window with optional window options.
    /// </summary>
    public RadarProcessingPressureWindow(RadarProcessingPressureWindowOptions? options = null)
    {
        Options = options ?? RadarProcessingPressureWindowOptions.Default;
    }

    /// <summary>
    /// Window capacity, eligibility, and hysteresis options.
    /// </summary>
    public RadarProcessingPressureWindowOptions Options { get; }

    /// <summary>
    /// Number of samples currently retained.
    /// </summary>
    public int SampleCount => samples.Count;

    /// <summary>
    /// Indicates whether any pressure samples have been retained.
    /// </summary>
    public bool HasSamples => SampleCount > 0;

    /// <summary>
    /// Indicates whether enough samples exist for rebalance planning.
    /// </summary>
    public bool IsRebalanceEligible => SampleCount >= Options.MinimumSampleCount;

    /// <summary>
    /// Topology version of the latest retained sample.
    /// </summary>
    public RadarProcessingTopologyVersion LatestTopologyVersion { get; private set; }

    /// <summary>
    /// Rolling shard pressure states ordered by shard id.
    /// </summary>
    public IReadOnlyList<RadarProcessingShardPressureState> Shards => shards;

    /// <summary>
    /// Rolling partition pressure states ordered by partition id.
    /// </summary>
    public IReadOnlyList<RadarProcessingPartitionPressureState> Partitions => partitions;

    /// <summary>
    /// Adds a compatible pressure sample and evicts oldest samples beyond capacity.
    /// </summary>
    public void AddSample(RadarProcessingPressureSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        EnsureCompatibleShape(sample);

        samples.Enqueue(sample);
        while (samples.Count > Options.SampleCapacity)
        {
            samples.Dequeue();
        }

        LatestTopologyVersion = sample.TopologyVersion;
        RebuildState();
    }

    /// <summary>
    /// Returns rolling state for a shard id.
    /// </summary>
    public RadarProcessingShardPressureState GetShard(int shardId)
    {
        if ((uint)shardId < (uint)shards.Count)
        {
            return shards[shardId];
        }

        throw new ArgumentOutOfRangeException(nameof(shardId));
    }

    /// <summary>
    /// Returns rolling state for a partition id.
    /// </summary>
    public RadarProcessingPartitionPressureState GetPartition(int partitionId)
    {
        if ((uint)partitionId < (uint)partitions.Count)
        {
            return partitions[partitionId];
        }

        throw new ArgumentOutOfRangeException(nameof(partitionId));
    }

    private void EnsureCompatibleShape(RadarProcessingPressureSample sample)
    {
        if (SampleCount == 0)
        {
            return;
        }

        var current = samples.Peek();
        if (sample.ShardCount != current.ShardCount)
        {
            throw new ArgumentException("Pressure sample shard count must match the pressure window shape.", nameof(sample));
        }

        if (sample.PartitionCount != current.PartitionCount)
        {
            throw new ArgumentException(
                "Pressure sample partition count must match the pressure window shape.",
                nameof(sample));
        }
    }

    private void RebuildState()
    {
        if (samples.Count == 0)
        {
            shards = Array.Empty<RadarProcessingShardPressureState>();
            partitions = Array.Empty<RadarProcessingPartitionPressureState>();
            return;
        }

        var previousShards = shards;
        var previousPartitions = partitions;
        var latest = samples.Last();
        var newShards = CreateShardStates(latest, previousShards);
        var newPartitions = CreatePartitionStates(latest, previousPartitions);

        shards = Array.AsReadOnly(newShards);
        partitions = Array.AsReadOnly(newPartitions);
    }

    private RadarProcessingShardPressureState[] CreateShardStates(
        RadarProcessingPressureSample latest,
        IReadOnlyList<RadarProcessingShardPressureState> previousShards)
    {
        var result = new RadarProcessingShardPressureState[latest.ShardCount];

        for (var shardId = 0; shardId < result.Length; shardId++)
        {
            var totalMetrics = RadarProcessingRouteMetrics.Empty;
            var scoreTotal = 0.0;

            foreach (var sample in samples)
            {
                var shard = sample.Shards[shardId];
                if (shard.ShardId != shardId)
                {
                    throw new InvalidOperationException("Pressure sample shard ids must be ordered by shard id.");
                }

                totalMetrics = totalMetrics.Add(shard.Metrics);
                scoreTotal += shard.Score.Value;
            }

            var latestShard = latest.Shards[shardId];
            var averageScore = new RadarProcessingPressureScore(scoreTotal / SampleCount);
            var previousBand = shardId < previousShards.Count
                ? previousShards[shardId].Band
                : RadarProcessingPressureBand.Cold;
            result[shardId] = new RadarProcessingShardPressureState(
                shardId,
                SampleCount,
                latestShard.PartitionCount,
                latestShard.ActivePartitionCount,
                totalMetrics,
                averageScore,
                Options.Classify(averageScore, previousBand));
        }

        return result;
    }

    private RadarProcessingPartitionPressureState[] CreatePartitionStates(
        RadarProcessingPressureSample latest,
        IReadOnlyList<RadarProcessingPartitionPressureState> previousPartitions)
    {
        var result = new RadarProcessingPartitionPressureState[latest.PartitionCount];

        for (var partitionId = 0; partitionId < result.Length; partitionId++)
        {
            var totalMetrics = RadarProcessingRouteMetrics.Empty;
            var scoreTotal = 0.0;

            foreach (var sample in samples)
            {
                var partition = sample.Partitions[partitionId];
                if (partition.PartitionId != partitionId)
                {
                    throw new InvalidOperationException(
                        "Pressure sample partition ids must be ordered by partition id.");
                }

                totalMetrics = totalMetrics.Add(partition.Metrics);
                scoreTotal += partition.Score.Value;
            }

            var latestPartition = latest.Partitions[partitionId];
            var averageScore = new RadarProcessingPressureScore(scoreTotal / SampleCount);
            var previousBand = partitionId < previousPartitions.Count
                ? previousPartitions[partitionId].Band
                : RadarProcessingPressureBand.Cold;
            result[partitionId] = new RadarProcessingPartitionPressureState(
                partitionId,
                latestPartition.ShardId,
                SampleCount,
                totalMetrics,
                averageScore,
                Options.Classify(averageScore, previousBand));
        }

        return result;
    }
}
