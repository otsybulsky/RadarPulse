namespace RadarPulse.Domain.Streaming;

public readonly record struct StreamSchemaVersion
{
    public static readonly StreamSchemaVersion Current = new(1);

    public StreamSchemaVersion(int value)
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
