using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

/// <summary>
/// Immutable partition-to-shard topology used to route processing batches.
/// </summary>
/// <remarks>
/// The topology fixes source count, partition count, shard count, and source-id
/// partition boundaries. A rebalance move creates a new topology version with a
/// changed partition owner while preserving all source-to-partition mapping.
/// </remarks>
public sealed class RadarProcessingTopology
{
    private readonly RadarProcessingPartitionAssignment[] partitions;
    private readonly IReadOnlyList<RadarProcessingPartitionAssignment> partitionView;

    /// <summary>
    /// Creates the initial topology for the source universe and processing options.
    /// </summary>
    public RadarProcessingTopology(
        RadarSourceUniverse sourceUniverse,
        RadarProcessingCoreOptions options)
        : this(
            sourceUniverse,
            options,
            RadarProcessingTopologyVersion.Initial)
    {
    }

    private RadarProcessingTopology(
        RadarSourceUniverse sourceUniverse,
        RadarProcessingCoreOptions options,
        RadarProcessingTopologyVersion version)
    {
        ArgumentNullException.ThrowIfNull(sourceUniverse);
        ArgumentNullException.ThrowIfNull(options);

        if (options.PartitionCount > sourceUniverse.SourceCount)
        {
            throw new ArgumentException(
                "Partition count must be less than or equal to source count.",
                nameof(options));
        }

        SourceUniverseVersion = sourceUniverse.Version;
        Version = version;
        SourceCount = sourceUniverse.SourceCount;
        PartitionCount = options.PartitionCount;
        ShardCount = options.ShardCount;
        partitions = CreatePartitions(SourceCount, PartitionCount, ShardCount);
        partitionView = Array.AsReadOnly(partitions);
    }

    private RadarProcessingTopology(
        SourceUniverseVersion sourceUniverseVersion,
        RadarProcessingTopologyVersion version,
        int sourceCount,
        int partitionCount,
        int shardCount,
        RadarProcessingPartitionAssignment[] partitions)
    {
        ArgumentNullException.ThrowIfNull(partitions);

        SourceUniverseVersion = sourceUniverseVersion;
        Version = version;
        SourceCount = sourceCount;
        PartitionCount = partitionCount;
        ShardCount = shardCount;
        this.partitions = partitions;
        partitionView = Array.AsReadOnly(partitions);
    }

    /// <summary>
    /// Source-universe version that routed batches must match.
    /// </summary>
    public SourceUniverseVersion SourceUniverseVersion { get; }

    /// <summary>
    /// Current topology version.
    /// </summary>
    public RadarProcessingTopologyVersion Version { get; }

    /// <summary>
    /// Number of sources in the source universe.
    /// </summary>
    public int SourceCount { get; }

    /// <summary>
    /// Number of stable source-id partitions.
    /// </summary>
    public int PartitionCount { get; }

    /// <summary>
    /// Number of processing shards that can own partitions.
    /// </summary>
    public int ShardCount { get; }

    /// <summary>
    /// Read-only partition assignments ordered by partition id.
    /// </summary>
    public IReadOnlyList<RadarProcessingPartitionAssignment> Partitions => partitionView;

    /// <summary>
    /// Returns the assignment for a partition id.
    /// </summary>
    public RadarProcessingPartitionAssignment GetPartition(int partitionId)
    {
        EnsurePartitionId(partitionId);
        return partitions[partitionId];
    }

    /// <summary>
    /// Maps a source id to its stable partition id.
    /// </summary>
    /// <remarks>
    /// Larger partitions receive the earliest source ranges when the source
    /// count is not evenly divisible by the partition count.
    /// </remarks>
    public int GetPartitionIdForSource(int sourceId)
    {
        EnsureSourceId(sourceId);

        var baseSourcesPerPartition = SourceCount / PartitionCount;
        var largerPartitionCount = SourceCount % PartitionCount;
        var largerPartitionSize = baseSourcesPerPartition + 1;
        var largerPartitionSourceCount = largerPartitionCount * largerPartitionSize;

        if (sourceId < largerPartitionSourceCount)
        {
            return sourceId / largerPartitionSize;
        }

        return largerPartitionCount +
               ((sourceId - largerPartitionSourceCount) / baseSourcesPerPartition);
    }

    /// <summary>
    /// Returns the partition assignment that owns the source id.
    /// </summary>
    public RadarProcessingPartitionAssignment GetPartitionForSource(int sourceId) =>
        partitions[GetPartitionIdForSource(sourceId)];

    /// <summary>
    /// Returns the current owner shard for a partition.
    /// </summary>
    public int GetShardIdForPartition(int partitionId) =>
        GetPartition(partitionId).ShardId;

    /// <summary>
    /// Returns the current owner shard for a source id.
    /// </summary>
    public int GetShardIdForSource(int sourceId) =>
        GetPartitionForSource(sourceId).ShardId;

    internal RadarProcessingTopology MovePartitionOwner(
        int partitionId,
        int targetShardId,
        RadarProcessingTopologyVersion version)
    {
        EnsurePartitionId(partitionId);
        EnsureShardId(targetShardId);

        var current = partitions[partitionId];
        var updated = (RadarProcessingPartitionAssignment[])partitions.Clone();
        updated[partitionId] = new RadarProcessingPartitionAssignment(
            current.PartitionId,
            targetShardId,
            current.SourceIdStart,
            current.SourceIdEndExclusive);

        return new RadarProcessingTopology(
            SourceUniverseVersion,
            version,
            SourceCount,
            PartitionCount,
            ShardCount,
            updated);
    }

    private static RadarProcessingPartitionAssignment[] CreatePartitions(
        int sourceCount,
        int partitionCount,
        int shardCount)
    {
        var result = new RadarProcessingPartitionAssignment[partitionCount];
        var baseSourcesPerPartition = sourceCount / partitionCount;
        var largerPartitionCount = sourceCount % partitionCount;
        var sourceIdStart = 0;

        for (var partitionId = 0; partitionId < partitionCount; partitionId++)
        {
            var partitionSourceCount =
                baseSourcesPerPartition + (partitionId < largerPartitionCount ? 1 : 0);
            var sourceIdEndExclusive = checked(sourceIdStart + partitionSourceCount);
            var shardId = MapPartitionToShard(partitionId, partitionCount, shardCount);

            result[partitionId] = new RadarProcessingPartitionAssignment(
                partitionId,
                shardId,
                sourceIdStart,
                sourceIdEndExclusive);

            sourceIdStart = sourceIdEndExclusive;
        }

        return result;
    }

    private static int MapPartitionToShard(
        int partitionId,
        int partitionCount,
        int shardCount) =>
        (int)(((long)partitionId * shardCount) / partitionCount);

    private void EnsurePartitionId(int partitionId)
    {
        if ((uint)partitionId < (uint)PartitionCount)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(partitionId));
    }

    private void EnsureSourceId(int sourceId)
    {
        if ((uint)sourceId < (uint)SourceCount)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(sourceId));
    }

    private void EnsureShardId(int shardId)
    {
        if ((uint)shardId < (uint)ShardCount)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(shardId));
    }
}
