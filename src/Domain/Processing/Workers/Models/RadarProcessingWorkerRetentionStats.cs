namespace RadarPulse.Domain.Processing;

/// <summary>
/// Counts retained and dropped worker telemetry detail after retention limits are applied.
/// </summary>
public sealed record RadarProcessingWorkerRetentionStats
{
    /// <summary>
    /// Creates retention counters for recent worker batch and failure detail.
    /// </summary>
    public RadarProcessingWorkerRetentionStats(
        long retainedBatchCount = 0,
        long droppedBatchCount = 0,
        long retainedFailureCount = 0,
        long droppedFailureCount = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(retainedBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(droppedBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(retainedFailureCount);
        ArgumentOutOfRangeException.ThrowIfNegative(droppedFailureCount);

        RetainedBatchCount = retainedBatchCount;
        DroppedBatchCount = droppedBatchCount;
        RetainedFailureCount = retainedFailureCount;
        DroppedFailureCount = droppedFailureCount;
    }

    /// <summary>
    /// Gets the number of recent batch samples retained.
    /// </summary>
    public long RetainedBatchCount { get; }

    /// <summary>
    /// Gets the number of recent batch samples dropped by retention limits.
    /// </summary>
    public long DroppedBatchCount { get; }

    /// <summary>
    /// Gets the number of recent failure samples retained.
    /// </summary>
    public long RetainedFailureCount { get; }

    /// <summary>
    /// Gets the number of recent failure samples dropped by retention limits.
    /// </summary>
    public long DroppedFailureCount { get; }

    /// <summary>
    /// Gets whether any diagnostic detail was dropped.
    /// </summary>
    public bool HasDroppedDetail =>
        DroppedBatchCount > 0 ||
        DroppedFailureCount > 0;
}
