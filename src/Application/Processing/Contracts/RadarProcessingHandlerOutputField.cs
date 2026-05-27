using RadarPulse.Domain.Processing;

namespace RadarPulse.Application.Processing;

/// <summary>
/// BFF-facing descriptor for one exported handler output field.
/// </summary>
public sealed class RadarProcessingHandlerOutputField
{
    /// <summary>
    /// Creates a handler output field descriptor.
    /// </summary>
    public RadarProcessingHandlerOutputField(
        int handlerIndex,
        string handlerName,
        string name,
        RadarSourceProcessingSnapshotFieldType type,
        int slotIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(handlerIndex);
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureKnownFieldType(type);
        ArgumentOutOfRangeException.ThrowIfNegative(slotIndex);

        HandlerIndex = handlerIndex;
        HandlerName = handlerName;
        Name = name;
        Type = type;
        SlotIndex = slotIndex;
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
    /// Exported field type.
    /// </summary>
    public RadarSourceProcessingSnapshotFieldType Type { get; }

    /// <summary>
    /// Handler-local slot index backing the field.
    /// </summary>
    public int SlotIndex { get; }

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
