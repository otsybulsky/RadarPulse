namespace RadarPulse.Domain.Processing;

/// <summary>
/// Reports current lifecycle, health, and capacity posture for an async worker group.
/// </summary>
public sealed record RadarProcessingWorkerGroupStatus
{
    /// <summary>
    /// Creates a worker group status and validates lifecycle, health, and error values.
    /// </summary>
    public RadarProcessingWorkerGroupStatus(
        RadarProcessingWorkerGroupState state = RadarProcessingWorkerGroupState.NotStarted,
        RadarProcessingWorkerHealth health = RadarProcessingWorkerHealth.NotReady,
        int workerCount = 0,
        int queueCapacity = 0,
        RadarProcessingWorkerLifecycleError lastError = RadarProcessingWorkerLifecycleError.None)
    {
        EnsureKnownState(state);
        EnsureKnownHealth(health);
        EnsureKnownLifecycleError(lastError);
        ArgumentOutOfRangeException.ThrowIfNegative(workerCount);
        ArgumentOutOfRangeException.ThrowIfNegative(queueCapacity);

        State = state;
        Health = health;
        WorkerCount = workerCount;
        QueueCapacity = queueCapacity;
        LastError = lastError;
    }

    /// <summary>
    /// Gets the lifecycle state of the worker group.
    /// </summary>
    public RadarProcessingWorkerGroupState State { get; }

    /// <summary>
    /// Gets the health posture of the worker group.
    /// </summary>
    public RadarProcessingWorkerHealth Health { get; }

    /// <summary>
    /// Gets the number of workers in the group.
    /// </summary>
    public int WorkerCount { get; }

    /// <summary>
    /// Gets the queue capacity configured for workers in the group.
    /// </summary>
    public int QueueCapacity { get; }

    /// <summary>
    /// Gets the last lifecycle error observed by the group.
    /// </summary>
    public RadarProcessingWorkerLifecycleError LastError { get; }

    /// <summary>
    /// Gets whether the group can accept a new dispatch request.
    /// </summary>
    public bool CanAcceptDispatch =>
        State == RadarProcessingWorkerGroupState.Running &&
        Health == RadarProcessingWorkerHealth.Healthy;

    internal static void EnsureKnownState(
        RadarProcessingWorkerGroupState state)
    {
        if (state is not RadarProcessingWorkerGroupState.NotStarted and
            not RadarProcessingWorkerGroupState.Running and
            not RadarProcessingWorkerGroupState.Stopping and
            not RadarProcessingWorkerGroupState.Stopped and
            not RadarProcessingWorkerGroupState.Faulted and
            not RadarProcessingWorkerGroupState.Disposed)
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
    }

    internal static void EnsureKnownHealth(
        RadarProcessingWorkerHealth health)
    {
        if (health is not RadarProcessingWorkerHealth.NotReady and
            not RadarProcessingWorkerHealth.Healthy and
            not RadarProcessingWorkerHealth.Draining and
            not RadarProcessingWorkerHealth.Faulted and
            not RadarProcessingWorkerHealth.Disposed)
        {
            throw new ArgumentOutOfRangeException(nameof(health));
        }
    }

    internal static void EnsureKnownLifecycleError(
        RadarProcessingWorkerLifecycleError error)
    {
        if (error is not RadarProcessingWorkerLifecycleError.None and
            not RadarProcessingWorkerLifecycleError.AlreadyStarted and
            not RadarProcessingWorkerLifecycleError.NotStarted and
            not RadarProcessingWorkerLifecycleError.NotRunning and
            not RadarProcessingWorkerLifecycleError.Stopping and
            not RadarProcessingWorkerLifecycleError.Stopped and
            not RadarProcessingWorkerLifecycleError.Faulted and
            not RadarProcessingWorkerLifecycleError.Disposed)
        {
            throw new ArgumentOutOfRangeException(nameof(error));
        }
    }
}
