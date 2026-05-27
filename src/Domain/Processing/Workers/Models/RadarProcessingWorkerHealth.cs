namespace RadarPulse.Domain.Processing;

/// <summary>
/// Reports the health posture of an async worker group.
/// </summary>
public enum RadarProcessingWorkerHealth : byte
{
    /// <summary>
    /// The worker group has not reached a dispatchable state.
    /// </summary>
    NotReady = 0,

    /// <summary>
    /// The worker group can accept and complete dispatch.
    /// </summary>
    Healthy = 1,

    /// <summary>
    /// The worker group is stopping and should not accept new work.
    /// </summary>
    Draining = 2,

    /// <summary>
    /// The worker group observed a failure and is not dispatchable.
    /// </summary>
    Faulted = 3,

    /// <summary>
    /// The worker group has been disposed.
    /// </summary>
    Disposed = 4
}
