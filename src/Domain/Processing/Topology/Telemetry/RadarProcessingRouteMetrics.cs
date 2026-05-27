namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingRouteMetrics
{
    public RadarProcessingRouteMetrics(
        long eventCount,
        long payloadValueCount,
        long rawValueChecksum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(eventCount);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadValueCount);
        ArgumentOutOfRangeException.ThrowIfNegative(rawValueChecksum);

        EventCount = eventCount;
        PayloadValueCount = payloadValueCount;
        RawValueChecksum = rawValueChecksum;
    }

    public long EventCount { get; }

    public long PayloadValueCount { get; }

    public long RawValueChecksum { get; }

    public static RadarProcessingRouteMetrics Empty => default;

    public RadarProcessingRouteMetrics AddEvent(RadarProcessingPayloadMetrics payloadMetrics) =>
        new(
            checked(EventCount + 1),
            checked(PayloadValueCount + payloadMetrics.PayloadValueCount),
            checked(RawValueChecksum + payloadMetrics.RawValueChecksum));

    public RadarProcessingRouteMetrics Add(RadarProcessingRouteMetrics other) =>
        new(
            checked(EventCount + other.EventCount),
            checked(PayloadValueCount + other.PayloadValueCount),
            checked(RawValueChecksum + other.RawValueChecksum));
}
