namespace RadarPulse.Domain.Processing;

/// <summary>
/// Represents the lifecycle state of an async worker group.
/// </summary>
public enum RadarProcessingWorkerGroupState : byte
{
    /// <summary>
    /// The worker group has not been started.
    /// </summary>
    NotStarted = 0,

    /// <summary>
    /// The worker group is accepting dispatch.
    /// </summary>
    Running = 1,

    /// <summary>
    /// The worker group is draining and no longer accepts new dispatch.
    /// </summary>
    Stopping = 2,

    /// <summary>
    /// The worker group has stopped.
    /// </summary>
    Stopped = 3,

    /// <summary>
    /// The worker group faulted and requires teardown or replacement.
    /// </summary>
    Faulted = 4,

    /// <summary>
    /// The worker group has been disposed.
    /// </summary>
    Disposed = 5
}
