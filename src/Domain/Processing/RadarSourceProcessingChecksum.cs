using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

internal static class RadarSourceProcessingChecksum
{
    public static ulong AppendEvent(
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

    public static ulong AppendSource(
        ulong checksum,
        int sourceId,
        long processedEventCount,
        long processedPayloadValueCount,
        long rawValueChecksum,
        long lastMessageTimestampUtcTicks,
        ulong processingChecksum)
    {
        checksum = RadarStreamChecksum.AppendInt32(checksum, sourceId);
        checksum = RadarStreamChecksum.AppendInt64(checksum, processedEventCount);
        checksum = RadarStreamChecksum.AppendInt64(checksum, processedPayloadValueCount);
        checksum = RadarStreamChecksum.AppendInt64(checksum, rawValueChecksum);
        checksum = RadarStreamChecksum.AppendInt64(checksum, lastMessageTimestampUtcTicks);
        return RadarStreamChecksum.AppendUInt64(checksum, processingChecksum);
    }
}
