namespace RadarPulse.Domain.Processing;

/// <summary>
/// Identifies why a worker group lifecycle operation was rejected.
/// </summary>
public enum RadarProcessingWorkerLifecycleError : byte
{
    /// <summary>
    /// No lifecycle error occurred.
    /// </summary>
    None = 0,

    /// <summary>
    /// Start was requested after the group had already started.
    /// </summary>
    AlreadyStarted = 1,

    /// <summary>
    /// The operation required a started group.
    /// </summary>
    NotStarted = 2,

    /// <summary>
    /// The operation required a running group.
    /// </summary>
    NotRunning = 3,

    /// <summary>
    /// The group is already stopping.
    /// </summary>
    Stopping = 4,

    /// <summary>
    /// The group is already stopped.
    /// </summary>
    Stopped = 5,

    /// <summary>
    /// The group is faulted.
    /// </summary>
    Faulted = 6,

    /// <summary>
    /// The group has been disposed.
    /// </summary>
    Disposed = 7
}
