namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingTopologyVersion
{
    public static RadarProcessingTopologyVersion Initial { get; } = new(0);

    public RadarProcessingTopologyVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        Value = value;
    }

    public long Value { get; }

    public RadarProcessingTopologyVersion Next() =>
        new(checked(Value + 1));

    public override string ToString() =>
        Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
