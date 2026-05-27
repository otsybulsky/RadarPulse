using RadarPulse.Domain.Processing;

namespace RadarPulse.Application.Processing;

public sealed class RadarProcessingHandlerOutputValueReadModel
{
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

    public int HandlerIndex { get; }

    public string HandlerName { get; }

    public string Name { get; }

    public RadarSourceProcessingSnapshotFieldType Type { get; }

    public long Int64Value { get; }

    public double DoubleValue { get; }

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

