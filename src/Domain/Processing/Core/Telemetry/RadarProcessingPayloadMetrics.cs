namespace RadarPulse.Domain.Processing;

/// <summary>
/// Represents per-payload value totals computed while reading a radar event payload.
/// </summary>
/// <param name="PayloadValueCount">Number of decoded radar payload values.</param>
/// <param name="RawValueChecksum">Additive checksum of decoded raw values.</param>
public readonly record struct RadarProcessingPayloadMetrics(
    long PayloadValueCount,
    long RawValueChecksum)
{
    /// <summary>
    /// Gets an all-zero payload metrics value.
    /// </summary>
    public static RadarProcessingPayloadMetrics Empty => default;

    /// <summary>
    /// Returns the checked sum of this metrics value and another value.
    /// </summary>
    public RadarProcessingPayloadMetrics Add(RadarProcessingPayloadMetrics other) =>
        new(
            checked(PayloadValueCount + other.PayloadValueCount),
            checked(RawValueChecksum + other.RawValueChecksum));
}
