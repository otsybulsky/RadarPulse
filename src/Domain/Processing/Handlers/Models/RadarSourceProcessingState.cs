namespace RadarPulse.Domain.Processing;

public ref struct RadarSourceProcessingState
{
    private readonly Span<long> int64Slots;
    private readonly Span<double> doubleSlots;

    internal RadarSourceProcessingState(
        Span<long> int64Slots,
        Span<double> doubleSlots)
    {
        this.int64Slots = int64Slots;
        this.doubleSlots = doubleSlots;
    }

    public int Int64SlotCount => int64Slots.Length;

    public int DoubleSlotCount => doubleSlots.Length;

    public long GetInt64(int slotIndex)
    {
        EnsureInt64Slot(slotIndex);
        return int64Slots[slotIndex];
    }

    public void SetInt64(int slotIndex, long value)
    {
        EnsureInt64Slot(slotIndex);
        int64Slots[slotIndex] = value;
    }

    public void AddInt64(int slotIndex, long value)
    {
        EnsureInt64Slot(slotIndex);
        int64Slots[slotIndex] = checked(int64Slots[slotIndex] + value);
    }

    public double GetDouble(int slotIndex)
    {
        EnsureDoubleSlot(slotIndex);
        return doubleSlots[slotIndex];
    }

    public void SetDouble(int slotIndex, double value)
    {
        EnsureDoubleSlot(slotIndex);
        doubleSlots[slotIndex] = value;
    }

    public void AddDouble(int slotIndex, double value)
    {
        EnsureDoubleSlot(slotIndex);
        doubleSlots[slotIndex] += value;
    }

    private void EnsureInt64Slot(int slotIndex)
    {
        if ((uint)slotIndex < (uint)int64Slots.Length)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(slotIndex));
    }

    private void EnsureDoubleSlot(int slotIndex)
    {
        if ((uint)slotIndex < (uint)doubleSlots.Length)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(slotIndex));
    }
}
