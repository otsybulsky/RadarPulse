namespace RadarPulse.Domain.Processing;

/// <summary>
/// Stable durable identifier for a persisted processing batch envelope.
/// </summary>
public readonly record struct RadarProcessingDurableBatchId
{
    /// <summary>
    /// Creates a non-empty durable batch id.
    /// </summary>
    public RadarProcessingDurableBatchId(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Durable batch id must not be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Durable batch id value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Returns the durable batch id value.
    /// </summary>
    public override string ToString() =>
        Value ?? string.Empty;
}
