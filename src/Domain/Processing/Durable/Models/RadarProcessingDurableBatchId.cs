namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingDurableBatchId
{
    public RadarProcessingDurableBatchId(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Durable batch id must not be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() =>
        Value ?? string.Empty;
}
