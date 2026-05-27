namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingWorkerGroupStatus
{
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

    public RadarProcessingWorkerGroupState State { get; }

    public RadarProcessingWorkerHealth Health { get; }

    public int WorkerCount { get; }

    public int QueueCapacity { get; }

    public RadarProcessingWorkerLifecycleError LastError { get; }

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
