namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingWorkerId
{
    public RadarProcessingWorkerId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString();
}
