namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingBenchmarkAllocationSnapshot
{
    public RadarProcessingBenchmarkAllocationSnapshot(long allocatedBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(allocatedBytes);

        AllocatedBytes = allocatedBytes;
    }

    public long AllocatedBytes { get; }

    public static RadarProcessingBenchmarkAllocationSnapshot Capture() =>
        new(GC.GetTotalAllocatedBytes(precise: true));

    public long DeltaSince(
        RadarProcessingBenchmarkAllocationSnapshot before) =>
        AllocatedBytes >= before.AllocatedBytes
            ? AllocatedBytes - before.AllocatedBytes
            : 0;
}
