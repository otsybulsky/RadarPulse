namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingBenchmarkAllocationSnapshot
{
    public RadarProcessingBenchmarkAllocationSnapshot(
        long allocatedBytes,
        bool isCurrentThread = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(allocatedBytes);

        AllocatedBytes = allocatedBytes;
        IsCurrentThread = isCurrentThread;
    }

    public long AllocatedBytes { get; }

    public bool IsCurrentThread { get; }

    public bool IsGlobal => !IsCurrentThread;

    public static RadarProcessingBenchmarkAllocationSnapshot Capture() =>
        new(GC.GetTotalAllocatedBytes(precise: true));

    public static RadarProcessingBenchmarkAllocationSnapshot CaptureCurrentThread() =>
        new(GC.GetAllocatedBytesForCurrentThread(), isCurrentThread: true);

    public long DeltaSince(
        RadarProcessingBenchmarkAllocationSnapshot before)
    {
        if (IsCurrentThread != before.IsCurrentThread)
        {
            throw new ArgumentException(
                "Allocation snapshot deltas require both snapshots to use the same allocation counter scope.",
                nameof(before));
        }

        return AllocatedBytes >= before.AllocatedBytes
            ? AllocatedBytes - before.AllocatedBytes
            : 0;
    }
}
