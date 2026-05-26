namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingHandlerDeltaValue
{
    private RadarProcessingHandlerDeltaValue(
        int sourceId,
        string fieldName,
        RadarSourceProcessingSnapshotFieldType type,
        long int64Value,
        double doubleValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        EnsureKnownType(type);

        SourceId = sourceId;
        FieldName = fieldName;
        Type = type;
        Int64Value = int64Value;
        DoubleValue = doubleValue;
    }

    public int SourceId { get; }

    public string FieldName { get; }

    public RadarSourceProcessingSnapshotFieldType Type { get; }

    public long Int64Value { get; }

    public double DoubleValue { get; }

    public static RadarProcessingHandlerDeltaValue ForInt64(
        int sourceId,
        string fieldName,
        long value) =>
        new(
            sourceId,
            fieldName,
            RadarSourceProcessingSnapshotFieldType.Int64,
            value,
            doubleValue: 0);

    public static RadarProcessingHandlerDeltaValue ForDouble(
        int sourceId,
        string fieldName,
        double value) =>
        new(
            sourceId,
            fieldName,
            RadarSourceProcessingSnapshotFieldType.Double,
            int64Value: 0,
            value);

    internal static void EnsureKnownType(
        RadarSourceProcessingSnapshotFieldType type)
    {
        if (type is not RadarSourceProcessingSnapshotFieldType.Int64 and
            not RadarSourceProcessingSnapshotFieldType.Double)
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}
