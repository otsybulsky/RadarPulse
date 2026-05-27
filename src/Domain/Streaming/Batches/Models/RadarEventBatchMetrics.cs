namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Deterministic counts and checksums for a radar event batch.
/// </summary>
public readonly record struct RadarEventBatchMetrics(
    /// <summary>
    /// Number of events in the batch.
    /// </summary>
    long EventCount,

    /// <summary>
    /// Number of payload bytes in the batch.
    /// </summary>
    long PayloadBytes,

    /// <summary>
    /// Number of payload gate values represented by the batch.
    /// </summary>
    long PayloadValueCount,

    /// <summary>
    /// Sum of raw payload values for deterministic validation.
    /// </summary>
    long RawValueChecksum,

    /// <summary>
    /// Checksum over batch header and event metadata.
    /// </summary>
    ulong MetadataChecksum)
{
    /// <summary>
    /// Computes deterministic metrics for a batch.
    /// </summary>
    public static RadarEventBatchMetrics Compute(RadarEventBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var events = batch.Events.Span;
        var payload = batch.Payload.Span;
        var hasCachedPayloadMetrics = batch.TryGetPayloadMetrics(
            out var payloadValueCount,
            out var rawValueChecksum);
        var metadataChecksum = ComputeHeaderChecksum(batch);

        for (var i = 0; i < events.Length; i++)
        {
            var streamEvent = events[i];
            if (!hasCachedPayloadMetrics)
            {
                payloadValueCount += streamEvent.GateCount;
                rawValueChecksum += SumRawValues(streamEvent, payload);
            }

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
