namespace RadarPulse.Infrastructure.Processing;

public readonly record struct RadarProcessingWorkerMailboxDequeueResult<TWork>
    where TWork : class
{
    public RadarProcessingWorkerMailboxDequeueResult(
        RadarProcessingWorkerMailboxDequeueStatus status,
        TWork? item = default)
    {
        EnsureKnownStatus(status);
        if (status == RadarProcessingWorkerMailboxDequeueStatus.Item &&
            item is null)
        {
            throw new ArgumentNullException(nameof(item), "Item dequeue result requires an item.");
        }

        if (status != RadarProcessingWorkerMailboxDequeueStatus.Item &&
            item is not null)
        {
            throw new ArgumentException("Non-item dequeue result cannot carry an item.", nameof(item));
        }

        Status = status;
        Item = item;
    }

    public RadarProcessingWorkerMailboxDequeueStatus Status { get; }

    public TWork? Item { get; }

    public bool HasItem => Status == RadarProcessingWorkerMailboxDequeueStatus.Item;

    internal static void EnsureKnownStatus(
        RadarProcessingWorkerMailboxDequeueStatus status)
    {
        if (status is not RadarProcessingWorkerMailboxDequeueStatus.Item and
            not RadarProcessingWorkerMailboxDequeueStatus.Closed and
            not RadarProcessingWorkerMailboxDequeueStatus.Canceled and
            not RadarProcessingWorkerMailboxDequeueStatus.Disposed)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
    }
}
