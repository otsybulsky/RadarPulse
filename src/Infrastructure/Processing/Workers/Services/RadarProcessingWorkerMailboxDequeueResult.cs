namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Result returned by a worker mailbox dequeue operation.
/// </summary>
/// <remarks>
/// Item results always carry a non-null item. Non-item results deliberately
/// carry no item so cancellation, close, and disposal paths cannot accidentally
/// process stale work.
/// </remarks>
public readonly record struct RadarProcessingWorkerMailboxDequeueResult<TWork>
    where TWork : class
{
    /// <summary>
    /// Creates a dequeue result and enforces item/status consistency.
    /// </summary>
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

    /// <summary>
    /// Status reported by the mailbox reader.
    /// </summary>
    public RadarProcessingWorkerMailboxDequeueStatus Status { get; }

    /// <summary>
    /// Work item returned for <see cref="RadarProcessingWorkerMailboxDequeueStatus.Item"/> results.
    /// </summary>
    public TWork? Item { get; }

    /// <summary>
    /// Indicates whether the dequeue operation produced a work item.
    /// </summary>
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
