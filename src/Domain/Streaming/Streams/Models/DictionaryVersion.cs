namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Monotonic version for dense dictionary visibility.
/// </summary>
public readonly record struct DictionaryVersion
{
    /// <summary>
    /// Initial dictionary version before any identity entries are visible.
    /// </summary>
    public static readonly DictionaryVersion Initial = new(1);

    /// <summary>
    /// Creates a positive dictionary version.
    /// </summary>
    public DictionaryVersion(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Numeric dictionary version value.
    /// </summary>
    public long Value { get; }

    /// <summary>
    /// Formats the numeric version value.
    /// </summary>
    public override string ToString() => Value.ToString();
}
