namespace RadarPulse.Domain.Processing;

public sealed partial class RadarSourceProcessingStateStore
{
    private long ReadInt64HandlerSlot(
        int sourceId,
        int slotIndex) =>
        handlerInt64Slots[GetSourceSlotOffset(sourceId, handlerSlotLayout.TotalInt64SlotCount, slotIndex)];

    private double ReadDoubleHandlerSlot(
        int sourceId,
        int slotIndex) =>
        handlerDoubleSlots[GetSourceSlotOffset(sourceId, handlerSlotLayout.TotalDoubleSlotCount, slotIndex)];

    private static int GetSourceSlotOffset(
        int sourceId,
        int sourceSlotCount,
        int slotOffset) =>
        checked((sourceId * sourceSlotCount) + slotOffset);

    private static long[] CreateInt64HandlerSlots(
        int sourceCount,
        int sourceSlotCount) =>
        sourceSlotCount == 0
            ? Array.Empty<long>()
            : new long[checked(sourceCount * sourceSlotCount)];

    private static double[] CreateDoubleHandlerSlots(
        int sourceCount,
        int sourceSlotCount) =>
        sourceSlotCount == 0
            ? Array.Empty<double>()
            : new double[checked(sourceCount * sourceSlotCount)];
}
