namespace RadarPulse.Domain.Processing;

public sealed class RadarSourceProcessingHandlerSlotLayout
{
    private readonly IReadOnlyList<IRadarSourceProcessingHandler> handlers;
    private readonly IReadOnlyList<RadarSourceProcessingHandlerSlotAssignment> assignments;

    public RadarSourceProcessingHandlerSlotLayout(
        IReadOnlyList<IRadarSourceProcessingHandler>? handlers)
    {
        if (handlers is null || handlers.Count == 0)
        {
            this.handlers = Array.Empty<IRadarSourceProcessingHandler>();
            assignments = Array.Empty<RadarSourceProcessingHandlerSlotAssignment>();
            return;
        }

        var handlerArray = new IRadarSourceProcessingHandler[handlers.Count];
        var assignmentArray = new RadarSourceProcessingHandlerSlotAssignment[handlers.Count];
        var snapshotFieldNames = new HashSet<string>(StringComparer.Ordinal);
        var int64SlotOffset = 0;
        var doubleSlotOffset = 0;
        var snapshotFieldCount = 0;

        for (var handlerIndex = 0; handlerIndex < handlers.Count; handlerIndex++)
        {
            var handler = handlers[handlerIndex];
            ArgumentNullException.ThrowIfNull(handler);
            ArgumentNullException.ThrowIfNull(handler.Descriptor);
            ValidateSnapshotFields(handler.Descriptor, snapshotFieldNames, ref snapshotFieldCount);

            handlerArray[handlerIndex] = handler;
            assignmentArray[handlerIndex] = new RadarSourceProcessingHandlerSlotAssignment(
                handler,
                handlerIndex,
                int64SlotOffset,
                doubleSlotOffset);

            int64SlotOffset = checked(int64SlotOffset + handler.Descriptor.Int64SlotCount);
            doubleSlotOffset = checked(doubleSlotOffset + handler.Descriptor.DoubleSlotCount);
        }

        this.handlers = Array.AsReadOnly(handlerArray);
        assignments = Array.AsReadOnly(assignmentArray);
        TotalInt64SlotCount = int64SlotOffset;
        TotalDoubleSlotCount = doubleSlotOffset;
        SnapshotFieldCount = snapshotFieldCount;
    }

    public IReadOnlyList<IRadarSourceProcessingHandler> Handlers => handlers;

    public IReadOnlyList<RadarSourceProcessingHandlerSlotAssignment> Assignments => assignments;

    public int TotalInt64SlotCount { get; }

    public int TotalDoubleSlotCount { get; }

    public int SnapshotFieldCount { get; }

    public bool HasHandlers => assignments.Count > 0;

    private static void ValidateSnapshotFields(
        RadarSourceProcessingHandlerDescriptor descriptor,
        HashSet<string> snapshotFieldNames,
        ref int snapshotFieldCount)
    {
        foreach (var field in descriptor.SnapshotFields)
        {
            if (!snapshotFieldNames.Add(field.Name))
            {
                throw new ArgumentException(
                    "Snapshot field names must be unique across all processing handlers.",
                    nameof(descriptor));
            }

            snapshotFieldCount = checked(snapshotFieldCount + 1);
        }
    }
}
