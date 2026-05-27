namespace RadarPulse.Domain.Processing;

/// <summary>
/// Immutable assignment of one source-id partition to its current owner shard.
/// </summary>
/// <remarks>
/// Source ranges are half-open and stable for the lifetime of a topology shape.
/// Rebalance moves change only <see cref="ShardId"/> for an existing partition;
/// they do not repartition source ids.
/// </remarks>
public readonly record struct RadarProcessingPartitionAssignment
{
    /// <summary>
    /// Creates a partition assignment for a non-empty source-id range.
    /// </summary>
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

    /// <summary>
    /// Zero-based partition id.
    /// </summary>
    public int PartitionId { get; }

    /// <summary>
    /// Current owner shard id for the partition.
    /// </summary>
    public int ShardId { get; }

    /// <summary>
    /// Inclusive first source id owned by the partition.
    /// </summary>
    public int SourceIdStart { get; }

    /// <summary>
    /// Exclusive upper source-id boundary for the partition.
    /// </summary>
    public int SourceIdEndExclusive { get; }

    /// <summary>
    /// Number of source ids covered by the assignment.
    /// </summary>
    public int SourceCount => SourceIdEndExclusive - SourceIdStart;

    /// <summary>
    /// Returns whether the source id falls inside this partition range.
    /// </summary>
    public bool ContainsSourceId(int sourceId) =>
        (uint)(sourceId - SourceIdStart) < (uint)SourceCount;
}
