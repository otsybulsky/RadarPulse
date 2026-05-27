namespace RadarPulse.Domain.Processing;

/// <summary>
/// Global slot layout for all handlers attached to one processing core.
/// </summary>
/// <remarks>
/// The layout assigns each handler a contiguous range within per-source Int64 and
/// Double state arrays. Snapshot field names must be unique across all handlers
/// because they become frontend-facing output names.
/// </remarks>
public sealed class RadarSourceProcessingHandlerSlotLayout
{
    private readonly IReadOnlyList<IRadarSourceProcessingHandler> handlers;
    private readonly IReadOnlyList<RadarSourceProcessingHandlerSlotAssignment> assignments;

    /// <summary>
    /// Creates a slot layout for the supplied handlers.
    /// </summary>
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

    /// <summary>
    /// Handlers in execution order.
    /// </summary>
    public IReadOnlyList<IRadarSourceProcessingHandler> Handlers => handlers;

    /// <summary>
    /// Slot assignments in handler execution order.
    /// </summary>
    public IReadOnlyList<RadarSourceProcessingHandlerSlotAssignment> Assignments => assignments;

    /// <summary>
    /// Total Int64 slots required per source across all handlers.
    /// </summary>
    public int TotalInt64SlotCount { get; }

    /// <summary>
    /// Total Double slots required per source across all handlers.
    /// </summary>
    public int TotalDoubleSlotCount { get; }

    /// <summary>
    /// Total exported snapshot field count across all handlers.
    /// </summary>
    public int SnapshotFieldCount { get; }

    /// <summary>
    /// Indicates whether any handler is attached to the processing core.
    /// </summary>
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
