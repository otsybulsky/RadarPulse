using RadarPulse.Domain.Processing;

namespace RadarPulse.Application.Processing;

public sealed class RadarProcessingHandlerOutputField
{
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

    public int HandlerIndex { get; }

    public string HandlerName { get; }

    public string Name { get; }

    public RadarSourceProcessingSnapshotFieldType Type { get; }

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
