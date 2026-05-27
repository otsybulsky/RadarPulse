namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Dispatch-level errors reported by the async worker group.
/// </summary>
public enum RadarProcessingAsyncWorkerGroupError : byte
{
    /// <summary>
    /// Dispatch completed without a worker group error.
    /// </summary>
    None = 0,

    /// <summary>
    /// Worker group was already started when a start transition was requested.
    /// </summary>
    AlreadyStarted = 1,

    /// <summary>
    /// Dispatch was requested before the worker group started.
    /// </summary>
    NotStarted = 2,

    /// <summary>
    /// Worker group lifecycle was not running.
    /// </summary>
    NotRunning = 3,

    /// <summary>
    /// Worker group was stopping.
    /// </summary>
    Stopping = 4,

    /// <summary>
    /// Worker group had already stopped.
    /// </summary>
    Stopped = 5,

    /// <summary>
    /// Worker group was faulted.
    /// </summary>
    Faulted = 6,

    /// <summary>
    /// Worker group was disposed.
    /// </summary>
    Disposed = 7,

    /// <summary>
    /// Another non-concurrent dispatch was already in flight.
    /// </summary>
    AlreadyInFlight = 8,

    /// <summary>
    /// A work item could not be enqueued to its worker mailbox.
    /// </summary>
    EnqueueRejected = 9,

    /// <summary>
    /// Batch timeout policy fired before dispatch completed.
    /// </summary>
    TimedOut = 10,

    /// <summary>
    /// The batch scope was closed before dispatch.
    /// </summary>
    ScopeClosed = 11
}
