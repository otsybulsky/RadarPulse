namespace RadarPulse.Domain.Processing;

/// <summary>
/// Ref-like source-local handler state view for one handler invocation.
/// </summary>
/// <remarks>
/// Slot indexes are relative to the handler descriptor, not the global handler
/// layout. The state view is valid only during the handler call.
/// </remarks>
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

    /// <summary>
    /// Number of Int64 slots available to the handler.
    /// </summary>
    public int Int64SlotCount => int64Slots.Length;

    /// <summary>
    /// Number of Double slots available to the handler.
    /// </summary>
    public int DoubleSlotCount => doubleSlots.Length;

    /// <summary>
    /// Reads an Int64 slot.
    /// </summary>
    public long GetInt64(int slotIndex)
    {
        EnsureInt64Slot(slotIndex);
        return int64Slots[slotIndex];
    }

    /// <summary>
    /// Writes an Int64 slot.
    /// </summary>
    public void SetInt64(int slotIndex, long value)
    {
        EnsureInt64Slot(slotIndex);
        int64Slots[slotIndex] = value;
    }

    /// <summary>
    /// Adds to an Int64 slot using checked arithmetic.
    /// </summary>
    public void AddInt64(int slotIndex, long value)
    {
        EnsureInt64Slot(slotIndex);
        int64Slots[slotIndex] = checked(int64Slots[slotIndex] + value);
    }

    /// <summary>
    /// Reads a Double slot.
    /// </summary>
    public double GetDouble(int slotIndex)
    {
        EnsureDoubleSlot(slotIndex);
        return doubleSlots[slotIndex];
    }

    /// <summary>
    /// Writes a Double slot.
    /// </summary>
    public void SetDouble(int slotIndex, double value)
    {
        EnsureDoubleSlot(slotIndex);
        doubleSlots[slotIndex] = value;
    }

    /// <summary>
    /// Adds to a Double slot.
    /// </summary>
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
