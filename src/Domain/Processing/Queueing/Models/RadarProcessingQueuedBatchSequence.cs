namespace RadarPulse.Domain.Processing;

/// <summary>
/// Monotonic provider sequence assigned to batches accepted by a queued provider.
/// </summary>
/// <remarks>
/// The sequence is the deterministic ordering key used to correlate enqueue,
/// processing, durable, telemetry, and product-facing batch evidence.
/// </remarks>
public readonly record struct RadarProcessingQueuedBatchSequence
{
    /// <summary>
    /// First provider sequence value.
    /// </summary>
    public static RadarProcessingQueuedBatchSequence Initial { get; } = new(0);

    /// <summary>
    /// Creates a non-negative provider sequence.
    /// </summary>
    public RadarProcessingQueuedBatchSequence(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        Value = value;
    }

    /// <summary>
    /// Numeric sequence value.
    /// </summary>
    public long Value { get; }

    /// <summary>
    /// Returns the next provider sequence.
    /// </summary>
    public RadarProcessingQueuedBatchSequence Next() =>
        new(checked(Value + 1));

    /// <summary>
    /// Formats the sequence using invariant culture.
    /// </summary>
    public override string ToString() =>
        Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
