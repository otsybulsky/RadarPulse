namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingWorkerRetentionStats
{
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

    public long RetainedBatchCount { get; }

    public long DroppedBatchCount { get; }

    public long RetainedFailureCount { get; }

    public long DroppedFailureCount { get; }

    public bool HasDroppedDetail =>
        DroppedBatchCount > 0 ||
        DroppedFailureCount > 0;
}
