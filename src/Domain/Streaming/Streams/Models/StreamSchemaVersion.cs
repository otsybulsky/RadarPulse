namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Version of the binary stream event and batch schema.
/// </summary>
public readonly record struct StreamSchemaVersion
{
    /// <summary>
    /// Current accepted stream schema version.
    /// </summary>
    public static readonly StreamSchemaVersion Current = new(1);

    /// <summary>
    /// Creates a positive stream schema version.
    /// </summary>
    public StreamSchemaVersion(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Numeric stream schema version value.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Formats the numeric version value.
    /// </summary>
    public override string ToString() => Value.ToString();
}
