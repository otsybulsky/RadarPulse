using RadarPulse.Domain.Processing;

namespace RadarPulse.Application.Processing;

/// <summary>
/// BFF-facing value for one exported handler output field on one source.
/// </summary>
public sealed class RadarProcessingHandlerOutputValueReadModel
{
    /// <summary>
    /// Creates a handler output value.
    /// </summary>
    public RadarProcessingHandlerOutputValueReadModel(
        int handlerIndex,
        string handlerName,
        string name,
        RadarSourceProcessingSnapshotFieldType type,
        long int64Value = 0,
        double doubleValue = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(handlerIndex);
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureKnownFieldType(type);

        HandlerIndex = handlerIndex;
        HandlerName = handlerName;
        Name = name;
        Type = type;
        Int64Value = int64Value;
        DoubleValue = doubleValue;
    }

    /// <summary>
    /// Handler index in configured handler order.
    /// </summary>
    public int HandlerIndex { get; }

    /// <summary>
    /// Handler name that owns the field.
    /// </summary>
    public string HandlerName { get; }

    /// <summary>
    /// Exported field name.
    /// </summary>
    public string Name { get; }

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
    /// Creates a read-model value from a contract field and matching snapshot value.
    /// </summary>
    public static RadarProcessingHandlerOutputValueReadModel FromSnapshotValue(
        RadarProcessingHandlerOutputField field,
        RadarSourceProcessingSnapshotValue value)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (!string.Equals(field.Name, value.Name, StringComparison.Ordinal))
        {
            throw new ArgumentException("Handler output field name must match snapshot value name.", nameof(value));
        }

        if (field.Type != value.Type)
        {
            throw new ArgumentException("Handler output field type must match snapshot value type.", nameof(value));
        }

        return new RadarProcessingHandlerOutputValueReadModel(
            field.HandlerIndex,
            field.HandlerName,
            field.Name,
            field.Type,
            value.Int64Value,
            value.DoubleValue);
    }

    private static void EnsureKnownFieldType(
        RadarSourceProcessingSnapshotFieldType type)
    {
        if (type is not RadarSourceProcessingSnapshotFieldType.Int64 and
            not RadarSourceProcessingSnapshotFieldType.Double)
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}
