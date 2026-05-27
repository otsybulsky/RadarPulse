namespace RadarPulse.Domain.Processing;

/// <summary>
/// Descriptor for one handler state slot exported into source snapshots.
/// </summary>
public readonly record struct RadarSourceProcessingSnapshotFieldDescriptor
{
    /// <summary>
    /// Creates a field descriptor bound to a handler-local slot.
    /// </summary>
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

    /// <summary>
    /// Stable exported field name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Field value type.
    /// </summary>
    public RadarSourceProcessingSnapshotFieldType Type { get; }

    /// <summary>
    /// Handler-local slot index for the field.
    /// </summary>
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
