namespace RadarPulse.Domain.Processing;

public readonly record struct RadarSourceProcessingSnapshotFieldDescriptor
{
    public RadarSourceProcessingSnapshotFieldDescriptor(
        string name,
        RadarSourceProcessingSnapshotFieldType type,
        int slotIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureKnownType(type);
        ArgumentOutOfRangeException.ThrowIfNegative(slotIndex);

        Name = name;
        Type = type;
        SlotIndex = slotIndex;
    }

    public string Name { get; }

    public RadarSourceProcessingSnapshotFieldType Type { get; }

    public int SlotIndex { get; }

    internal static void EnsureKnownType(RadarSourceProcessingSnapshotFieldType type)
    {
        if (type is not RadarSourceProcessingSnapshotFieldType.Int64 and
            not RadarSourceProcessingSnapshotFieldType.Double)
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}
