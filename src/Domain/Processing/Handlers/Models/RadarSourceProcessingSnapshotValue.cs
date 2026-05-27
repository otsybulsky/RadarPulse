namespace RadarPulse.Domain.Processing;

public readonly record struct RadarSourceProcessingSnapshotValue
{
    private RadarSourceProcessingSnapshotValue(
        string name,
        RadarSourceProcessingSnapshotFieldType type,
        long int64Value,
        double doubleValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        RadarSourceProcessingSnapshotFieldDescriptor.EnsureKnownType(type);

        Name = name;
        Type = type;
        Int64Value = int64Value;
        DoubleValue = doubleValue;
    }

    public string Name { get; }

    public RadarSourceProcessingSnapshotFieldType Type { get; }

    public long Int64Value { get; }

    public double DoubleValue { get; }

    public static RadarSourceProcessingSnapshotValue FromInt64(
        string name,
        long value) =>
        new(
            name,
            RadarSourceProcessingSnapshotFieldType.Int64,
            value,
            doubleValue: 0);

    public static RadarSourceProcessingSnapshotValue FromDouble(
        string name,
        double value) =>
        new(
            name,
            RadarSourceProcessingSnapshotFieldType.Double,
            int64Value: 0,
            doubleValue: value);
}
