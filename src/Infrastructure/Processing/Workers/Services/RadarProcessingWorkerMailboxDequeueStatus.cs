namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Outcome of waiting for a worker mailbox item.
/// </summary>
public enum RadarProcessingWorkerMailboxDequeueStatus : byte
{
    /// <summary>
    /// A work item was read from the mailbox.
    /// </summary>
    Item = 1,

    /// <summary>
    /// The mailbox was closed after all accepted work had drained.
    /// </summary>
    Closed = 2,

    /// <summary>
    /// The read operation observed caller cancellation.
    /// </summary>
    Canceled = 3,

    /// <summary>
    /// The mailbox was disposed while the reader was waiting or after an item was read.
    /// </summary>
    Disposed = 4
}
