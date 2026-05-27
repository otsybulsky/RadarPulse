namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Version of a source-universe layout.
/// </summary>
public readonly record struct SourceUniverseVersion
{
    /// <summary>
    /// Initial source-universe version.
    /// </summary>
    public static readonly SourceUniverseVersion Initial = new(1);

    /// <summary>
    /// Creates a positive source-universe version.
    /// </summary>
    public SourceUniverseVersion(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Numeric source-universe version value.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Formats the numeric version value.
    /// </summary>
    public override string ToString() => Value.ToString();
}
