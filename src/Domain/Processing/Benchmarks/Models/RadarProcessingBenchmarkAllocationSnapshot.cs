namespace RadarPulse.Domain.Processing;

/// <summary>
/// Captures an allocation counter value used to compute benchmark allocation deltas.
/// </summary>
public readonly record struct RadarProcessingBenchmarkAllocationSnapshot
{
    /// <summary>
    /// Creates an allocation snapshot for either global or current-thread allocation counters.
    /// </summary>
    public RadarProcessingBenchmarkAllocationSnapshot(
        long allocatedBytes,
        bool isCurrentThread = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(allocatedBytes);

        AllocatedBytes = allocatedBytes;
        IsCurrentThread = isCurrentThread;
    }

    /// <summary>
    /// Gets the captured allocated byte count.
    /// </summary>
    public long AllocatedBytes { get; }

    /// <summary>
    /// Gets whether the snapshot came from the current-thread allocation counter.
    /// </summary>
    public bool IsCurrentThread { get; }

    /// <summary>
    /// Gets whether the snapshot came from the global allocation counter.
    /// </summary>
    public bool IsGlobal => !IsCurrentThread;

    /// <summary>
    /// Captures the process-wide allocated byte counter.
    /// </summary>
    public static RadarProcessingBenchmarkAllocationSnapshot Capture() =>
        new(GC.GetTotalAllocatedBytes(precise: true));

    /// <summary>
    /// Captures the current-thread allocated byte counter.
    /// </summary>
    public static RadarProcessingBenchmarkAllocationSnapshot CaptureCurrentThread() =>
        new(GC.GetAllocatedBytesForCurrentThread(), isCurrentThread: true);

    /// <summary>
    /// Computes a non-negative allocation delta from a previous snapshot with the same counter scope.
    /// </summary>
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
