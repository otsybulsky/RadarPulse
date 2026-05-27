namespace RadarPulse.Domain.Processing;

/// <summary>
/// Slot offset assignment for one handler inside the global handler layout.
/// </summary>
public sealed class RadarSourceProcessingHandlerSlotAssignment
{
    internal RadarSourceProcessingHandlerSlotAssignment(
        IRadarSourceProcessingHandler handler,
        int handlerIndex,
        int int64SlotOffset,
        int doubleSlotOffset)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentOutOfRangeException.ThrowIfNegative(handlerIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(int64SlotOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(doubleSlotOffset);

        Handler = handler;
        HandlerIndex = handlerIndex;
        Int64SlotOffset = int64SlotOffset;
        DoubleSlotOffset = doubleSlotOffset;
    }

    /// <summary>
    /// Handler that owns the assigned slot ranges.
    /// </summary>
    public IRadarSourceProcessingHandler Handler { get; }

    /// <summary>
    /// Handler descriptor.
    /// </summary>
    public RadarSourceProcessingHandlerDescriptor Descriptor => Handler.Descriptor;

    /// <summary>
    /// Zero-based handler index in execution order.
    /// </summary>
    public int HandlerIndex { get; }

    /// <summary>
    /// Int64 slot offset within each source's global Int64 handler state.
    /// </summary>
    public int Int64SlotOffset { get; }

    /// <summary>
    /// Double slot offset within each source's global Double handler state.
    /// </summary>
    public int DoubleSlotOffset { get; }
}
