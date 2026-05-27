namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Outcome of reading from the owned provider batch queue.
/// </summary>
public enum RadarProcessingOwnedBatchDequeueStatus
{
    /// <summary>
    /// A queued batch was read and removed from pending retained-resource counts.
    /// </summary>
    Item = 1,

    /// <summary>
    /// The queue closed after all accepted batches were drained.
    /// </summary>
    Closed = 2,

    /// <summary>
    /// The read observed caller cancellation.
    /// </summary>
    Canceled = 3,

    /// <summary>
    /// The queue was faulted and carries the fault message.
    /// </summary>
    Faulted = 4,

    /// <summary>
    /// The queue was disposed before an item could be returned.
    /// </summary>
    Disposed = 5
}
