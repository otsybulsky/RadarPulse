namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingQueuedBatchSequence
{
    public static RadarProcessingQueuedBatchSequence Initial { get; } = new(0);

    public RadarProcessingQueuedBatchSequence(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        Value = value;
    }

    public long Value { get; }

    public RadarProcessingQueuedBatchSequence Next() =>
        new(checked(Value + 1));

    public override string ToString() =>
        Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
