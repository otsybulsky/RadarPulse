namespace RadarPulse.Domain.Processing;

/// <summary>
/// Monotonic version assigned to a processing topology snapshot.
/// </summary>
/// <remarks>
/// The version starts at zero and advances only when a partition owner changes.
/// Processing routes, telemetry, pressure samples, and rebalance decisions carry
/// this value so callers can reject stale moves and validate that artifacts were
/// produced from the same topology.
/// </remarks>
public readonly record struct RadarProcessingTopologyVersion
{
    /// <summary>
    /// Version used by a newly created topology before any rebalance move.
    /// </summary>
    public static RadarProcessingTopologyVersion Initial { get; } = new(0);

    /// <summary>
    /// Creates a topology version from a non-negative numeric value.
    /// </summary>
    public RadarProcessingTopologyVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        Value = value;
    }

    /// <summary>
    /// Numeric topology version value.
    /// </summary>
    public long Value { get; }

    /// <summary>
    /// Returns the next topology version after a successful owner move.
    /// </summary>
    public RadarProcessingTopologyVersion Next() =>
        new(checked(Value + 1));

    /// <summary>
    /// Formats the numeric version value using invariant culture.
    /// </summary>
    public override string ToString() =>
        Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
