namespace RadarPulse.Domain.Processing;

/// <summary>
/// Outcome of one attempt to enqueue an owned batch into a provider queue.
/// </summary>
public enum RadarProcessingQueuedBatchEnqueueStatus
{
    /// <summary>
    /// The queue accepted and retained the owned batch.
    /// </summary>
    Accepted = 1,

    /// <summary>
    /// The queue was at capacity and did not accept the batch.
    /// </summary>
    Full = 2,

    /// <summary>
    /// The enqueue wait exceeded the configured timeout.
    /// </summary>
    TimedOut = 3,

    /// <summary>
    /// The enqueue attempt was canceled.
    /// </summary>
    Canceled = 4,

    /// <summary>
    /// The queue was closed to new work.
    /// </summary>
    Closed = 5,

    /// <summary>
    /// The queue was faulted and could not accept work.
    /// </summary>
    Faulted = 6
}
