using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

/// <summary>
/// Captured processing state for a partition before or after ownership handoff.
/// </summary>
/// <remarks>
/// Snapshots aggregate counters and checksums from the source processing state
/// store without mutating it. Rebalance handoff validation compares a before
/// snapshot against a projected or published after snapshot to ensure moving the
/// owner shard did not alter source state.
/// </remarks>
public sealed class RadarProcessingPartitionStateSnapshot
{
    /// <summary>
    /// Creates a partition state snapshot with aggregate counters and checksums.
    /// </summary>
    public RadarProcessingPartitionStateSnapshot(
        int partitionId,
        int shardId,
        int sourceIdStart,
        int sourceIdEndExclusive,
        long activeSourceCount,
        long processedEventCount,
        long processedPayloadValueCount,
        long rawValueChecksum,
        RadarProcessingPartitionStateChecksum checksum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(partitionId);
        ArgumentOutOfRangeException.ThrowIfNegative(shardId);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceIdStart);
        ArgumentOutOfRangeException.ThrowIfNegative(activeSourceCount);
        ArgumentOutOfRangeException.ThrowIfNegative(processedEventCount);
        ArgumentOutOfRangeException.ThrowIfNegative(processedPayloadValueCount);
        ArgumentOutOfRangeException.ThrowIfNegative(rawValueChecksum);

        if (sourceIdEndExclusive <= sourceIdStart)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceIdEndExclusive),
                sourceIdEndExclusive,
                "Source range must contain at least one source id.");
        }

        if (activeSourceCount > sourceIdEndExclusive - sourceIdStart)
        {
            throw new ArgumentOutOfRangeException(
                nameof(activeSourceCount),
                activeSourceCount,
                "Active source count cannot exceed the partition source count.");
        }

        PartitionId = partitionId;
        ShardId = shardId;
        SourceIdStart = sourceIdStart;
        SourceIdEndExclusive = sourceIdEndExclusive;
        ActiveSourceCount = activeSourceCount;
        ProcessedEventCount = processedEventCount;
        ProcessedPayloadValueCount = processedPayloadValueCount;
        RawValueChecksum = rawValueChecksum;
        Checksum = checksum;
    }

    /// <summary>
    /// Partition id represented by the snapshot.
    /// </summary>
    public int PartitionId { get; }

    /// <summary>
    /// Owner shard id associated with the snapshot.
    /// </summary>
    public int ShardId { get; }

    /// <summary>
    /// Inclusive first source id covered by the snapshot.
    /// </summary>
    public int SourceIdStart { get; }

    /// <summary>
    /// Exclusive upper source-id boundary covered by the snapshot.
    /// </summary>
    public int SourceIdEndExclusive { get; }

    /// <summary>
    /// Number of source ids in the snapshot range.
    /// </summary>
    public int SourceCount => SourceIdEndExclusive - SourceIdStart;

    /// <summary>
    /// Number of sources in the range with active processing state.
    /// </summary>
    public long ActiveSourceCount { get; }

    /// <summary>
    /// Total processed event count across active sources.
    /// </summary>
    public long ProcessedEventCount { get; }

    /// <summary>
    /// Total processed payload value count across active sources.
    /// </summary>
    public long ProcessedPayloadValueCount { get; }

    /// <summary>
    /// Total raw value checksum across active sources.
    /// </summary>
    public long RawValueChecksum { get; }

    /// <summary>
    /// Deterministic checksums for state categories validated during handoff.
    /// </summary>
    public RadarProcessingPartitionStateChecksum Checksum { get; }

    /// <summary>
    /// Captures current state-store aggregates for a partition assignment.
    /// </summary>
    /// <returns>
    /// Snapshot that can be compared before and after a rebalance owner move.
    /// </returns>
    public static RadarProcessingPartitionStateSnapshot Capture(
        RadarProcessingPartitionAssignment partition,
        RadarSourceProcessingStateStore stateStore)
    {
        ArgumentNullException.ThrowIfNull(stateStore);

        if (partition.SourceIdEndExclusive > stateStore.SourceCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(partition),
                partition.SourceIdEndExclusive,
                "Partition source range must fit inside the processing state store.");
        }

        var activeSourceCount = 0L;
        var processedEventCount = 0L;
        var processedPayloadValueCount = 0L;
        var rawValueChecksum = 0L;
        var processingChecksum = 0UL;
        var lastMessageTimestampChecksum = 0UL;
        var handlerSnapshotChecksum = 0UL;
        var includeHandlerSnapshots = stateStore.HandlerSlotLayout.HasHandlers;

        for (var sourceId = partition.SourceIdStart; sourceId < partition.SourceIdEndExclusive; sourceId++)
        {
            var snapshot = stateStore.GetSnapshot(sourceId);
            if (!snapshot.IsActive)
            {
                continue;
            }

            if (activeSourceCount == 0)
            {
                processingChecksum = RadarStreamChecksum.Initial;
                lastMessageTimestampChecksum = RadarStreamChecksum.Initial;
                if (includeHandlerSnapshots)
                {
                    handlerSnapshotChecksum = RadarStreamChecksum.Initial;
                }
            }

            activeSourceCount = checked(activeSourceCount + 1);
            processedEventCount = checked(processedEventCount + snapshot.ProcessedEventCount);
            processedPayloadValueCount = checked(
                processedPayloadValueCount + snapshot.ProcessedPayloadValueCount);
            rawValueChecksum = checked(rawValueChecksum + snapshot.RawValueChecksum);
            processingChecksum = RadarSourceProcessingChecksum.AppendSource(
                processingChecksum,
                sourceId,
                snapshot.ProcessedEventCount,
                snapshot.ProcessedPayloadValueCount,
                snapshot.RawValueChecksum,
                snapshot.LastMessageTimestampUtcTicks,
                snapshot.ProcessingChecksum);
            lastMessageTimestampChecksum = AppendLastMessageTimestamp(
                lastMessageTimestampChecksum,
                sourceId,
                snapshot.LastMessageTimestampUtcTicks);

            if (includeHandlerSnapshots)
            {
                handlerSnapshotChecksum = AppendHandlerSnapshot(
                    handlerSnapshotChecksum,
                    stateStore.GetHandlerSnapshot(sourceId));
            }
        }

        return new RadarProcessingPartitionStateSnapshot(
            partition.PartitionId,
            partition.ShardId,
            partition.SourceIdStart,
            partition.SourceIdEndExclusive,
            activeSourceCount,
            processedEventCount,
            processedPayloadValueCount,
            rawValueChecksum,
            new RadarProcessingPartitionStateChecksum(
                processingChecksum,
                lastMessageTimestampChecksum,
                handlerSnapshotChecksum));
    }

    private static ulong AppendLastMessageTimestamp(
        ulong checksum,
        int sourceId,
        long lastMessageTimestampUtcTicks)
    {
        checksum = RadarStreamChecksum.AppendInt32(checksum, sourceId);
        return RadarStreamChecksum.AppendInt64(checksum, lastMessageTimestampUtcTicks);
    }

    private static ulong AppendHandlerSnapshot(
        ulong checksum,
        RadarSourceProcessingHandlerSnapshot snapshot)
    {
        checksum = RadarStreamChecksum.AppendInt32(checksum, snapshot.SourceId);
        checksum = RadarStreamChecksum.AppendInt32(checksum, snapshot.Values.Count);

        foreach (var value in snapshot.Values)
        {
            checksum = RadarStreamChecksum.AppendStringOrdinal(checksum, value.Name);
            checksum = RadarStreamChecksum.AppendInt32(checksum, (int)value.Type);
            checksum = value.Type switch
            {
                RadarSourceProcessingSnapshotFieldType.Int64 =>
                    RadarStreamChecksum.AppendInt64(checksum, value.Int64Value),
                RadarSourceProcessingSnapshotFieldType.Double =>
                    RadarStreamChecksum.AppendUInt64(
                        checksum,
                        unchecked((ulong)BitConverter.DoubleToInt64Bits(value.DoubleValue))),
                _ => throw new InvalidOperationException("Unsupported handler snapshot field type.")
            };
        }

        return checksum;
    }
}
