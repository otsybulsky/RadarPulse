namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingPressureSample
{
    private readonly IReadOnlyList<RadarProcessingShardPressureSample> shards;
    private readonly IReadOnlyList<RadarProcessingPartitionPressureSample> partitions;

    private RadarProcessingPressureSample(
        RadarProcessingTopologyVersion topologyVersion,
        RadarProcessingRouteMetrics batchMetrics,
        RadarProcessingShardPressureSample[] shards,
        RadarProcessingPartitionPressureSample[] partitions)
    {
        ArgumentNullException.ThrowIfNull(shards);
        ArgumentNullException.ThrowIfNull(partitions);

        if (shards.Length == 0)
        {
            throw new ArgumentException("Pressure sample must contain at least one shard.", nameof(shards));
        }

        if (partitions.Length == 0)
        {
            throw new ArgumentException("Pressure sample must contain at least one partition.", nameof(partitions));
        }

        TopologyVersion = topologyVersion;
        BatchMetrics = batchMetrics;
        this.shards = Array.AsReadOnly((RadarProcessingShardPressureSample[])shards.Clone());
        this.partitions = Array.AsReadOnly((RadarProcessingPartitionPressureSample[])partitions.Clone());
    }

    public RadarProcessingTopologyVersion TopologyVersion { get; }

    public RadarProcessingRouteMetrics BatchMetrics { get; }

    public int ShardCount => shards.Count;

    public int PartitionCount => partitions.Count;

    public IReadOnlyList<RadarProcessingShardPressureSample> Shards => shards;

    public IReadOnlyList<RadarProcessingPartitionPressureSample> Partitions => partitions;

    public static RadarProcessingPressureSample FromTelemetry(
        RadarProcessingTelemetry telemetry,
        RadarProcessingPressureOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        options ??= RadarProcessingPressureOptions.Default;

        var shards = new RadarProcessingShardPressureSample[telemetry.ShardCount];
        for (var shardId = 0; shardId < shards.Length; shardId++)
        {
            var shard = telemetry.Shards[shardId];
            var score = options.Score(shard.Metrics);
            shards[shardId] = new RadarProcessingShardPressureSample(
                shard.ShardId,
                shard.PartitionCount,
                shard.ActivePartitionCount,
                shard.Metrics,
                score,
                options.Classify(score));
        }

        var partitions = new RadarProcessingPartitionPressureSample[telemetry.PartitionCount];
        for (var partitionId = 0; partitionId < partitions.Length; partitionId++)
        {
            var partition = telemetry.Partitions[partitionId];
            var score = options.Score(partition.Metrics);
            partitions[partitionId] = new RadarProcessingPartitionPressureSample(
                partition.PartitionId,
                partition.ShardId,
                partition.Metrics,
                score,
                options.Classify(score));
        }

        return new RadarProcessingPressureSample(
            telemetry.TopologyVersion,
            telemetry.BatchMetrics,
            shards,
            partitions);
    }
}
