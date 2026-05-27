namespace RadarPulse.Domain.Streaming;

public readonly record struct SourceUniverseVersion
{
    public static readonly SourceUniverseVersion Initial = new(1);

    public SourceUniverseVersion(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString();
}
