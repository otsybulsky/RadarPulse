namespace RadarPulse.Domain.Processing;

/// <summary>
/// Declares one handler's source-local state layout and exported snapshot fields.
/// </summary>
/// <remarks>
/// Descriptor names and snapshot field names are ordinal identifiers used by
/// read models and product output. Slot indexes must be valid within the
/// descriptor's own int64 or double slot count.
/// </remarks>
public sealed class RadarSourceProcessingHandlerDescriptor
{
    private readonly IReadOnlyList<RadarSourceProcessingSnapshotFieldDescriptor> snapshotFields;

    /// <summary>
    /// Creates a handler descriptor with validated slot counts and snapshot fields.
    /// </summary>
    public RadarSourceProcessingHandlerDescriptor(
        string name,
        int int64SlotCount,
        int doubleSlotCount,
        IReadOnlyList<RadarSourceProcessingSnapshotFieldDescriptor>? snapshotFields = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(int64SlotCount);
        ArgumentOutOfRangeException.ThrowIfNegative(doubleSlotCount);

        Name = name;
        Int64SlotCount = int64SlotCount;
        DoubleSlotCount = doubleSlotCount;
        this.snapshotFields = CloneAndValidateSnapshotFields(
            snapshotFields,
            int64SlotCount,
            doubleSlotCount);
    }

    /// <summary>
    /// Stable handler name exposed through contracts and read models.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Number of source-local Int64 slots owned by this handler.
    /// </summary>
    public int Int64SlotCount { get; }

    /// <summary>
    /// Number of source-local Double slots owned by this handler.
    /// </summary>
    public int DoubleSlotCount { get; }

    /// <summary>
    /// Exported fields mapped to handler state slots.
    /// </summary>
    public IReadOnlyList<RadarSourceProcessingSnapshotFieldDescriptor> SnapshotFields => snapshotFields;

    private static IReadOnlyList<RadarSourceProcessingSnapshotFieldDescriptor> CloneAndValidateSnapshotFields(
        IReadOnlyList<RadarSourceProcessingSnapshotFieldDescriptor>? snapshotFields,
        int int64SlotCount,
        int doubleSlotCount)
    {
        if (snapshotFields is null || snapshotFields.Count == 0)
        {
            return Array.Empty<RadarSourceProcessingSnapshotFieldDescriptor>();
        }

        var result = new RadarSourceProcessingSnapshotFieldDescriptor[snapshotFields.Count];
        var fieldNames = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < result.Length; i++)
        {
            var field = snapshotFields[i];
            ValidateFieldSlot(field, int64SlotCount, doubleSlotCount);
            if (!fieldNames.Add(field.Name))
            {
                throw new ArgumentException(
                    "Snapshot field names must be unique within a handler descriptor.",
                    nameof(snapshotFields));
            }

            result[i] = field;
        }

        return Array.AsReadOnly(result);
    }

    private static void ValidateFieldSlot(
        RadarSourceProcessingSnapshotFieldDescriptor field,
        int int64SlotCount,
        int doubleSlotCount)
    {
        switch (field.Type)
        {
            case RadarSourceProcessingSnapshotFieldType.Int64:
                if ((uint)field.SlotIndex >= (uint)int64SlotCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(field));
                }

                return;

            case RadarSourceProcessingSnapshotFieldType.Double:
                if ((uint)field.SlotIndex >= (uint)doubleSlotCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(field));
                }

                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(field));
        }
    }
}
