namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Outcome of a non-blocking worker mailbox enqueue attempt.
/// </summary>
public enum RadarProcessingWorkerMailboxEnqueueStatus : byte
{
    /// <summary>
    /// The work item was accepted by the mailbox.
    /// </summary>
    Accepted = 1,

    /// <summary>
    /// The bounded mailbox had no immediate capacity for the work item.
    /// </summary>
    Full = 2,

    /// <summary>
    /// The mailbox was closed to new writers before the item could be accepted.
    /// </summary>
    Closed = 3,

    /// <summary>
    /// The mailbox was disposed before the item could be accepted.
    /// </summary>
    Disposed = 4
}
