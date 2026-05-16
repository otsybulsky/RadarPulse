using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public sealed class RadarSourceProcessingStateStore
{
    private readonly bool[] activeSources;
    private readonly long[] processedEventCounts;
    private readonly long[] processedPayloadValueCounts;
    private readonly long[] rawValueChecksums;
    private readonly long[] lastMessageTimestampUtcTicks;
    private readonly ulong[] processingChecksums;

    public RadarSourceProcessingStateStore(RadarSourceUniverse sourceUniverse)
    {
        ArgumentNullException.ThrowIfNull(sourceUniverse);

        SourceUniverseVersion = sourceUniverse.Version;
        SourceCount = sourceUniverse.SourceCount;

        activeSources = new bool[SourceCount];
        processedEventCounts = new long[SourceCount];
        processedPayloadValueCounts = new long[SourceCount];
        rawValueChecksums = new long[SourceCount];
        lastMessageTimestampUtcTicks = new long[SourceCount];
        processingChecksums = new ulong[SourceCount];
    }

    public SourceUniverseVersion SourceUniverseVersion { get; }

    public int SourceCount { get; }

    public long ActiveSourceCount { get; private set; }

    public void ApplyProcessedEvent(
        in RadarStreamEvent streamEvent,
        long processedPayloadValueCount,
        long rawValueChecksum)
    {
        var sourceId = streamEvent.SourceId;
        EnsureSourceId(sourceId);
        ArgumentOutOfRangeException.ThrowIfNegative(processedPayloadValueCount);
        ArgumentOutOfRangeException.ThrowIfNegative(rawValueChecksum);

        var isActive = activeSources[sourceId];
        if (isActive &&
            streamEvent.MessageTimestampUtcTicks < lastMessageTimestampUtcTicks[sourceId])
        {
            throw new InvalidOperationException(
                "Source-local events must be applied by non-decreasing message timestamp.");
        }

        if (!isActive)
        {
            activeSources[sourceId] = true;
            ActiveSourceCount = checked(ActiveSourceCount + 1);
        }

        processedEventCounts[sourceId] = checked(processedEventCounts[sourceId] + 1);
        processedPayloadValueCounts[sourceId] = checked(
            processedPayloadValueCounts[sourceId] + processedPayloadValueCount);
        rawValueChecksums[sourceId] = checked(rawValueChecksums[sourceId] + rawValueChecksum);
        lastMessageTimestampUtcTicks[sourceId] = streamEvent.MessageTimestampUtcTicks;
        processingChecksums[sourceId] = AppendEventChecksum(
            isActive ? processingChecksums[sourceId] : RadarStreamChecksum.Initial,
            streamEvent,
            processedPayloadValueCount,
            rawValueChecksum);
    }

    public RadarSourceProcessingSnapshot GetSnapshot(int sourceId)
    {
        EnsureSourceId(sourceId);

        return new RadarSourceProcessingSnapshot(
            sourceId,
            activeSources[sourceId],
            processedEventCounts[sourceId],
            processedPayloadValueCounts[sourceId],
            rawValueChecksums[sourceId],
            lastMessageTimestampUtcTicks[sourceId],
            processingChecksums[sourceId]);
    }

    public RadarSourceProcessingSnapshot[] CreateSnapshots()
    {
        var snapshots = new RadarSourceProcessingSnapshot[SourceCount];
        for (var sourceId = 0; sourceId < snapshots.Length; sourceId++)
        {
            snapshots[sourceId] = GetSnapshot(sourceId);
        }

        return snapshots;
    }

    public RadarProcessingMetrics CreateMetrics(long processedBatchCount = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(processedBatchCount);

        var processedStreamEventCount = 0L;
        var processedPayloadValueCount = 0L;
        var rawValueChecksum = 0L;
        var processingChecksum = ActiveSourceCount == 0
            ? 0UL
            : RadarStreamChecksum.Initial;

        for (var sourceId = 0; sourceId < SourceCount; sourceId++)
        {
            if (!activeSources[sourceId])
            {
                continue;
            }

            processedStreamEventCount = checked(
                processedStreamEventCount + processedEventCounts[sourceId]);
            processedPayloadValueCount = checked(
                processedPayloadValueCount + processedPayloadValueCounts[sourceId]);
            rawValueChecksum = checked(rawValueChecksum + rawValueChecksums[sourceId]);
            processingChecksum = AppendSourceChecksum(processingChecksum, sourceId);
        }

        return new RadarProcessingMetrics(
            processedBatchCount,
            processedStreamEventCount,
            processedPayloadValueCount,
            ActiveSourceCount,
            rawValueChecksum,
            processingChecksum);
    }

    private static ulong AppendEventChecksum(
        ulong checksum,
        in RadarStreamEvent streamEvent,
        long processedPayloadValueCount,
        long rawValueChecksum)
    {
        checksum = RadarStreamChecksum.AppendInt64(checksum, streamEvent.MessageTimestampUtcTicks);
        checksum = RadarStreamChecksum.AppendInt32(checksum, streamEvent.SourceId);
        checksum = RadarStreamChecksum.AppendInt32(checksum, streamEvent.SourceRecord);
        checksum = RadarStreamChecksum.AppendInt32(checksum, streamEvent.SourceMessage);
        checksum = RadarStreamChecksum.AppendInt32(checksum, streamEvent.RadialSequence);
        checksum = RadarStreamChecksum.AppendUInt16(checksum, streamEvent.MomentId);
        checksum = RadarStreamChecksum.AppendUInt16(checksum, streamEvent.GateStart);
        checksum = RadarStreamChecksum.AppendUInt16(checksum, streamEvent.GateCount);
        checksum = RadarStreamChecksum.AppendInt64(checksum, processedPayloadValueCount);
        return RadarStreamChecksum.AppendInt64(checksum, rawValueChecksum);
    }

    private ulong AppendSourceChecksum(ulong checksum, int sourceId)
    {
        checksum = RadarStreamChecksum.AppendInt32(checksum, sourceId);
        checksum = RadarStreamChecksum.AppendInt64(checksum, processedEventCounts[sourceId]);
        checksum = RadarStreamChecksum.AppendInt64(checksum, processedPayloadValueCounts[sourceId]);
        checksum = RadarStreamChecksum.AppendInt64(checksum, rawValueChecksums[sourceId]);
        checksum = RadarStreamChecksum.AppendInt64(checksum, lastMessageTimestampUtcTicks[sourceId]);
        return RadarStreamChecksum.AppendUInt64(checksum, processingChecksums[sourceId]);
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
