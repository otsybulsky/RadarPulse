namespace RadarPulse.Domain.Processing;

/// <summary>
/// Stable identity for one handler delta.
/// </summary>
/// <remarks>
/// Delta ids are derived from handler contract identity and batch identity so
/// duplicate detection can distinguish identical replays from conflicting deltas.
/// </remarks>
public readonly record struct RadarProcessingHandlerDeltaId
{
    /// <summary>
    /// Creates a non-empty handler delta id.
    /// </summary>
    public RadarProcessingHandlerDeltaId(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Handler delta id must not be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Handler delta id value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Returns the delta id value.
    /// </summary>
    public override string ToString() =>
        Value ?? string.Empty;
}
