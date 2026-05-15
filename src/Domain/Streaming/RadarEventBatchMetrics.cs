namespace RadarPulse.Domain.Streaming;

public readonly record struct RadarEventBatchMetrics(
    long EventCount,
    long PayloadBytes,
    long PayloadValueCount,
    long RawValueChecksum,
    ulong MetadataChecksum)
{
    public static RadarEventBatchMetrics Compute(RadarEventBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var events = batch.Events.Span;
        var payload = batch.Payload.Span;
        var payloadValueCount = 0L;
        var rawValueChecksum = 0L;
        var metadataChecksum = ComputeHeaderChecksum(batch);

        for (var i = 0; i < events.Length; i++)
        {
            var streamEvent = events[i];
            payloadValueCount += streamEvent.GateCount;
            rawValueChecksum += SumRawValues(streamEvent, payload);
            metadataChecksum = AppendEventMetadata(metadataChecksum, streamEvent);
        }

        return new RadarEventBatchMetrics(
            events.Length,
            batch.PayloadLength,
            payloadValueCount,
            rawValueChecksum,
            metadataChecksum);
    }

    private static ulong ComputeHeaderChecksum(RadarEventBatch batch)
    {
        var checksum = RadarStreamChecksum.Initial;
        checksum = RadarStreamChecksum.AppendInt32(checksum, batch.StreamSchemaVersion.Value);
        checksum = RadarStreamChecksum.AppendInt64(checksum, batch.DictionaryVersion.Value);
        checksum = RadarStreamChecksum.AppendInt32(checksum, batch.SourceUniverseVersion.Value);
        checksum = RadarStreamChecksum.AppendInt32(checksum, batch.EventCount);
        return RadarStreamChecksum.AppendInt32(checksum, batch.PayloadLength);
    }

    private static ulong AppendEventMetadata(ulong checksum, RadarStreamEvent streamEvent)
    {
        checksum = RadarStreamChecksum.AppendInt64(checksum, streamEvent.VolumeTimestampUtcTicks);
        checksum = RadarStreamChecksum.AppendInt64(checksum, streamEvent.MessageTimestampUtcTicks);
        checksum = RadarStreamChecksum.AppendInt32(checksum, streamEvent.SourceId);
        checksum = RadarStreamChecksum.AppendInt32(checksum, streamEvent.SourceRecord);
        checksum = RadarStreamChecksum.AppendInt32(checksum, streamEvent.SourceMessage);
        checksum = RadarStreamChecksum.AppendInt32(checksum, streamEvent.RadialSequence);
        checksum = RadarStreamChecksum.AppendInt32(checksum, streamEvent.PayloadOffset);
        checksum = RadarStreamChecksum.AppendInt32(checksum, streamEvent.PayloadLength);
        checksum = RadarStreamChecksum.AppendSingle(checksum, streamEvent.Scale);
        checksum = RadarStreamChecksum.AppendSingle(checksum, streamEvent.Offset);
        checksum = RadarStreamChecksum.AppendUInt16(checksum, streamEvent.RadarOrdinal);
        checksum = RadarStreamChecksum.AppendUInt16(checksum, streamEvent.MomentId);
        checksum = RadarStreamChecksum.AppendUInt16(checksum, streamEvent.ElevationSlot);
        checksum = RadarStreamChecksum.AppendUInt16(checksum, streamEvent.AzimuthBucket);
        checksum = RadarStreamChecksum.AppendUInt16(checksum, streamEvent.RangeBand);
        checksum = RadarStreamChecksum.AppendUInt16(checksum, streamEvent.GateStart);
        checksum = RadarStreamChecksum.AppendUInt16(checksum, streamEvent.GateCount);
        checksum = RadarStreamChecksum.AppendByte(checksum, (byte)streamEvent.WordSize);
        return RadarStreamChecksum.AppendByte(checksum, (byte)streamEvent.StatusModel);
    }

    private static long SumRawValues(RadarStreamEvent streamEvent, ReadOnlySpan<byte> payload)
    {
        var eventPayload = payload.Slice(streamEvent.PayloadOffset, streamEvent.PayloadLength);
        var checksum = 0L;

        switch (streamEvent.WordSize)
        {
            case RadarStreamWordSize.EightBit:
                for (var valueIndex = 0; valueIndex < eventPayload.Length; valueIndex++)
                {
                    checksum += eventPayload[valueIndex];
                }

                return checksum;

            case RadarStreamWordSize.SixteenBit:
                for (var valueIndex = 0; valueIndex < eventPayload.Length; valueIndex += sizeof(ushort))
                {
                    checksum += (eventPayload[valueIndex] << 8) | eventPayload[valueIndex + 1];
                }

                return checksum;

            default:
                throw new InvalidOperationException("Unsupported radar stream word size.");
        }
    }
}
