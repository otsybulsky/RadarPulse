namespace RadarPulse.Domain.Streaming;

public readonly record struct DictionaryVersion
{
    public static readonly DictionaryVersion Initial = new(1);

    public DictionaryVersion(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        Value = value;
    }

    public long Value { get; }

    public override string ToString() => Value.ToString();
}
