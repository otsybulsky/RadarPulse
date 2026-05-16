namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingPartitionAssignment
{
    public RadarProcessingPartitionAssignment(
        int partitionId,
        int shardId,
        int sourceIdStart,
        int sourceIdEndExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(partitionId);
        ArgumentOutOfRangeException.ThrowIfNegative(shardId);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceIdStart);

        if (sourceIdEndExclusive <= sourceIdStart)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceIdEndExclusive),
                sourceIdEndExclusive,
                "Source range must contain at least one source id.");
        }

        PartitionId = partitionId;
        ShardId = shardId;
        SourceIdStart = sourceIdStart;
        SourceIdEndExclusive = sourceIdEndExclusive;
    }

    public int PartitionId { get; }

    public int ShardId { get; }

    public int SourceIdStart { get; }

    public int SourceIdEndExclusive { get; }

    public int SourceCount => SourceIdEndExclusive - SourceIdStart;

    public bool ContainsSourceId(int sourceId) =>
        (uint)(sourceId - SourceIdStart) < (uint)SourceCount;
}
