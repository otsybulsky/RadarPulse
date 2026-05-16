using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingTopology
{
    private readonly RadarProcessingPartitionAssignment[] partitions;

    public RadarProcessingTopology(
        RadarSourceUniverse sourceUniverse,
        RadarProcessingCoreOptions options)
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
        SourceCount = sourceUniverse.SourceCount;
        PartitionCount = options.PartitionCount;
        ShardCount = options.ShardCount;
        partitions = CreatePartitions(SourceCount, PartitionCount, ShardCount);
    }

    public SourceUniverseVersion SourceUniverseVersion { get; }

    public int SourceCount { get; }

    public int PartitionCount { get; }

    public int ShardCount { get; }

    public IReadOnlyList<RadarProcessingPartitionAssignment> Partitions => partitions;

    public RadarProcessingPartitionAssignment GetPartition(int partitionId)
    {
        EnsurePartitionId(partitionId);
        return partitions[partitionId];
    }

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

    public RadarProcessingPartitionAssignment GetPartitionForSource(int sourceId) =>
        partitions[GetPartitionIdForSource(sourceId)];

    public int GetShardIdForPartition(int partitionId) =>
        GetPartition(partitionId).ShardId;

    public int GetShardIdForSource(int sourceId) =>
        GetPartitionForSource(sourceId).ShardId;

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
}
