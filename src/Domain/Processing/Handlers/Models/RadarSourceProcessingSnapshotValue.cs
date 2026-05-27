namespace RadarPulse.Domain.Processing;

/// <summary>
/// One exported handler snapshot value for a source.
/// </summary>
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

    /// <summary>
    /// Exported field name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Field value type.
    /// </summary>
    public RadarSourceProcessingSnapshotFieldType Type { get; }

    /// <summary>
    /// Int64 value when <see cref="Type"/> is Int64.
    /// </summary>
    public long Int64Value { get; }

    /// <summary>
    /// Double value when <see cref="Type"/> is Double.
    /// </summary>
    public double DoubleValue { get; }

    /// <summary>
    /// Creates an Int64 snapshot value.
    /// </summary>
    public static RadarSourceProcessingSnapshotValue FromInt64(
        string name,
        long value) =>
        new(
            name,
            RadarSourceProcessingSnapshotFieldType.Int64,
            value,
            doubleValue: 0);

    /// <summary>
    /// Creates a Double snapshot value.
    /// </summary>
    public static RadarSourceProcessingSnapshotValue FromDouble(
        string name,
        double value) =>
        new(
            name,
            RadarSourceProcessingSnapshotFieldType.Double,
            int64Value: 0,
            doubleValue: value);
}
