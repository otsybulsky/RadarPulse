namespace RadarPulse.Domain.Processing;

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

    public IRadarSourceProcessingHandler Handler { get; }

    public RadarSourceProcessingHandlerDescriptor Descriptor => Handler.Descriptor;

    public int HandlerIndex { get; }

    public int Int64SlotOffset { get; }

    public int DoubleSlotOffset { get; }
}
