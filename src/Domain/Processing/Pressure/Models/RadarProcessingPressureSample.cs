namespace RadarPulse.Domain.Processing;

/// <summary>
/// Pressure snapshot derived from processing telemetry for one topology version.
/// </summary>
/// <remarks>
/// Samples keep shard and partition entries ordered by id. The rolling pressure
/// window depends on this ordering when rebuilding aggregate state.
/// </remarks>
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

    /// <summary>
    /// Topology version represented by this pressure sample.
    /// </summary>
    public RadarProcessingTopologyVersion TopologyVersion { get; }

    /// <summary>
    /// Batch-level route metrics used to create the sample.
    /// </summary>
    public RadarProcessingRouteMetrics BatchMetrics { get; }

    /// <summary>
    /// Number of shard pressure entries.
    /// </summary>
    public int ShardCount => shards.Count;

    /// <summary>
    /// Number of partition pressure entries.
    /// </summary>
    public int PartitionCount => partitions.Count;

    /// <summary>
    /// Shard pressure entries ordered by shard id.
    /// </summary>
    public IReadOnlyList<RadarProcessingShardPressureSample> Shards => shards;

    /// <summary>
    /// Partition pressure entries ordered by partition id.
    /// </summary>
    public IReadOnlyList<RadarProcessingPartitionPressureSample> Partitions => partitions;

    /// <summary>
    /// Creates a pressure sample from caller-provided shard and partition entries.
    /// </summary>
    public static RadarProcessingPressureSample Create(
        RadarProcessingTopologyVersion topologyVersion,
        RadarProcessingRouteMetrics batchMetrics,
        IReadOnlyList<RadarProcessingShardPressureSample> shards,
        IReadOnlyList<RadarProcessingPartitionPressureSample> partitions)
    {
        ArgumentNullException.ThrowIfNull(shards);
        ArgumentNullException.ThrowIfNull(partitions);

        return new RadarProcessingPressureSample(
            topologyVersion,
            batchMetrics,
            shards.ToArray(),
            partitions.ToArray());
    }

    /// <summary>
    /// Creates a pressure sample from processing telemetry and scoring options.
    /// </summary>
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
