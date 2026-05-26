namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingHandlerDeltaId
{
    public RadarProcessingHandlerDeltaId(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Handler delta id must not be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() =>
        Value ?? string.Empty;
}
