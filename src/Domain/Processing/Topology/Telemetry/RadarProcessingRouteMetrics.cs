namespace RadarPulse.Domain.Processing;

/// <summary>
/// Aggregate payload metrics for a routed set of events.
/// </summary>
public readonly record struct RadarProcessingRouteMetrics
{
    /// <summary>
    /// Creates route metrics from non-negative event and payload totals.
    /// </summary>
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

    /// <summary>
    /// Number of routed events.
    /// </summary>
    public long EventCount { get; }

    /// <summary>
    /// Number of payload values represented by routed events.
    /// </summary>
    public long PayloadValueCount { get; }

    /// <summary>
    /// Deterministic raw-value checksum for routed payload values.
    /// </summary>
    public long RawValueChecksum { get; }

    /// <summary>
    /// Empty metrics value.
    /// </summary>
    public static RadarProcessingRouteMetrics Empty => default;

    /// <summary>
    /// Returns metrics with one event's payload metrics added.
    /// </summary>
    public RadarProcessingRouteMetrics AddEvent(RadarProcessingPayloadMetrics payloadMetrics) =>
        new(
            checked(EventCount + 1),
            checked(PayloadValueCount + payloadMetrics.PayloadValueCount),
            checked(RawValueChecksum + payloadMetrics.RawValueChecksum));

    /// <summary>
    /// Returns metrics with another route metric aggregate added.
    /// </summary>
    public RadarProcessingRouteMetrics Add(RadarProcessingRouteMetrics other) =>
        new(
            checked(EventCount + other.EventCount),
            checked(PayloadValueCount + other.PayloadValueCount),
            checked(RawValueChecksum + other.RawValueChecksum));
}
