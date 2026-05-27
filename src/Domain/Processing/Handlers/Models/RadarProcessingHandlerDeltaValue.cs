namespace RadarPulse.Domain.Processing;

/// <summary>
/// One source/field value carried by a mergeable handler delta.
/// </summary>
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

    /// <summary>
    /// Source id affected by this value.
    /// </summary>
    public int SourceId { get; }

    /// <summary>
    /// Exported handler field name.
    /// </summary>
    public string FieldName { get; }

    /// <summary>
    /// Value type.
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
    /// Creates an Int64 delta value.
    /// </summary>
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

    /// <summary>
    /// Creates a Double delta value.
    /// </summary>
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
