namespace RadarPulse.Domain.Processing;

/// <summary>
/// Lifecycle state for a queued-provider processing session.
/// </summary>
public enum RadarProcessingQueuedSessionStatus
{
    /// <summary>
    /// The session has not started.
    /// </summary>
    NotStarted = 0,

    /// <summary>
    /// The session is accepting or processing queued work.
    /// </summary>
    Running = 1,

    /// <summary>
    /// The session is no longer accepting new work and is draining accepted work.
    /// </summary>
    Draining = 2,

    /// <summary>
    /// The session completed accepted work successfully.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// The session reached a faulted terminal state.
    /// </summary>
    Faulted = 4,

    /// <summary>
    /// The session was canceled.
    /// </summary>
    Canceled = 5,

    /// <summary>
    /// The session has released its runtime resources.
    /// </summary>
    Disposed = 6
}
