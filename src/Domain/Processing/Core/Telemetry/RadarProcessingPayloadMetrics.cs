namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingPayloadMetrics(
    long PayloadValueCount,
    long RawValueChecksum)
{
    public static RadarProcessingPayloadMetrics Empty => default;

    public RadarProcessingPayloadMetrics Add(RadarProcessingPayloadMetrics other) =>
        new(
            checked(PayloadValueCount + other.PayloadValueCount),
            checked(RawValueChecksum + other.RawValueChecksum));
}
