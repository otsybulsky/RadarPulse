using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

/// <summary>
/// Reads radar event payload slices and computes deterministic payload metrics.
/// </summary>
public static class RadarProcessingPayloadReader
{
    /// <summary>
    /// Computes aggregate payload metrics for every event in a batch.
    /// </summary>
    public static RadarProcessingPayloadMetrics ComputeBatchMetrics(RadarEventBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var metrics = RadarProcessingPayloadMetrics.Empty;
        var events = batch.Events.Span;
        var payload = batch.Payload.Span;

        for (var i = 0; i < events.Length; i++)
        {
            metrics = metrics.Add(ComputeEventMetrics(events[i], payload));
        }

        return metrics;
    }

    /// <summary>
    /// Computes gate count and raw value checksum for one event payload.
    /// </summary>
    public static RadarProcessingPayloadMetrics ComputeEventMetrics(
        RadarStreamEvent streamEvent,
        ReadOnlySpan<byte> batchPayload)
    {
        var eventPayload = GetEventPayload(streamEvent, batchPayload);
        var rawValueChecksum = streamEvent.WordSize switch
        {
            RadarStreamWordSize.EightBit => SumEightBitValues(eventPayload),
            RadarStreamWordSize.SixteenBit => SumSixteenBitValues(eventPayload),
            _ => throw new InvalidOperationException("Unsupported radar stream word size.")
        };

        return new RadarProcessingPayloadMetrics(streamEvent.GateCount, rawValueChecksum);
    }

    /// <summary>
    /// Returns the payload slice referenced by an event after validating offset, length, and word size.
    /// </summary>
    public static ReadOnlySpan<byte> GetEventPayload(
        RadarStreamEvent streamEvent,
        ReadOnlySpan<byte> batchPayload)
    {
        EnsureSupportedWordSize(streamEvent.WordSize);

        if (streamEvent.PayloadLength != streamEvent.ExpectedPayloadLength)
        {
            throw new ArgumentException(
                "Event payload length does not match gate count and word size.",
                nameof(streamEvent));
        }

        if (streamEvent.PayloadLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(streamEvent));
        }

        if (streamEvent.PayloadOffset < 0 ||
            streamEvent.PayloadOffset > batchPayload.Length ||
            streamEvent.PayloadLength > batchPayload.Length - streamEvent.PayloadOffset)
        {
            throw new ArgumentOutOfRangeException(
                nameof(streamEvent),
                "Event payload reference exceeds batch payload storage.");
        }

        return batchPayload.Slice(streamEvent.PayloadOffset, streamEvent.PayloadLength);
    }

    private static void EnsureSupportedWordSize(RadarStreamWordSize wordSize)
    {
        if (wordSize is not RadarStreamWordSize.EightBit and not RadarStreamWordSize.SixteenBit)
        {
            throw new ArgumentOutOfRangeException(nameof(wordSize));
        }
    }

    private static long SumEightBitValues(ReadOnlySpan<byte> eventPayload)
    {
        var checksum = 0L;
        for (var valueIndex = 0; valueIndex < eventPayload.Length; valueIndex++)
        {
            checksum += eventPayload[valueIndex];
        }

        return checksum;
    }

    private static long SumSixteenBitValues(ReadOnlySpan<byte> eventPayload)
    {
        var checksum = 0L;
        for (var valueIndex = 0; valueIndex < eventPayload.Length; valueIndex += sizeof(ushort))
        {
            checksum += (eventPayload[valueIndex] << 8) | eventPayload[valueIndex + 1];
        }

        return checksum;
    }
}
