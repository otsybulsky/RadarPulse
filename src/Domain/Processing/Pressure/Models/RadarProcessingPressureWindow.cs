namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingPressureWindow
{
    private readonly Queue<RadarProcessingPressureSample> samples = new();
    private IReadOnlyList<RadarProcessingShardPressureState> shards =
        Array.Empty<RadarProcessingShardPressureState>();
    private IReadOnlyList<RadarProcessingPartitionPressureState> partitions =
        Array.Empty<RadarProcessingPartitionPressureState>();

    public RadarProcessingPressureWindow(RadarProcessingPressureWindowOptions? options = null)
    {
        Options = options ?? RadarProcessingPressureWindowOptions.Default;
    }

    public RadarProcessingPressureWindowOptions Options { get; }

    public int SampleCount => samples.Count;

    public bool HasSamples => SampleCount > 0;

    public bool IsRebalanceEligible => SampleCount >= Options.MinimumSampleCount;

    public RadarProcessingTopologyVersion LatestTopologyVersion { get; private set; }

    public IReadOnlyList<RadarProcessingShardPressureState> Shards => shards;

    public IReadOnlyList<RadarProcessingPartitionPressureState> Partitions => partitions;

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

    public RadarProcessingShardPressureState GetShard(int shardId)
    {
        if ((uint)shardId < (uint)shards.Count)
        {
            return shards[shardId];
        }

        throw new ArgumentOutOfRangeException(nameof(shardId));
    }

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
