namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingWorkerGroupLifecycle
{
    private readonly int workerCount;
    private readonly int queueCapacity;
    private RadarProcessingWorkerGroupState state;
    private RadarProcessingWorkerHealth health;
    private RadarProcessingWorkerLifecycleError lastError;

    public RadarProcessingWorkerGroupLifecycle(
        RadarProcessingAsyncExecutionOptions? options = null)
    {
        var effectiveOptions = options ?? RadarProcessingAsyncExecutionOptions.Default;

        workerCount = effectiveOptions.WorkerCount;
        queueCapacity = effectiveOptions.QueueCapacity;
        state = RadarProcessingWorkerGroupState.NotStarted;
        health = RadarProcessingWorkerHealth.NotReady;
        lastError = RadarProcessingWorkerLifecycleError.None;
    }

    public RadarProcessingWorkerGroupStatus Status =>
        new(state, health, workerCount, queueCapacity, lastError);

    public RadarProcessingWorkerLifecycleResult Start()
    {
        if (state == RadarProcessingWorkerGroupState.NotStarted)
        {
            return TransitionTo(
                RadarProcessingWorkerGroupState.Running,
                RadarProcessingWorkerHealth.Healthy);
        }

        return Fail(RejectForCurrentState(starting: true));
    }

    public RadarProcessingWorkerLifecycleResult StopAccepting()
    {
        if (state == RadarProcessingWorkerGroupState.Running)
        {
            return TransitionTo(
                RadarProcessingWorkerGroupState.Stopping,
                RadarProcessingWorkerHealth.Draining);
        }

        if (state == RadarProcessingWorkerGroupState.Stopping)
        {
            return Succeed();
        }

        return Fail(RejectForCurrentState());
    }

    public RadarProcessingWorkerLifecycleResult Stop()
    {
        if (state is RadarProcessingWorkerGroupState.Running or RadarProcessingWorkerGroupState.Stopping)
        {
            return TransitionTo(
                RadarProcessingWorkerGroupState.Stopped,
                RadarProcessingWorkerHealth.NotReady);
        }

        if (state == RadarProcessingWorkerGroupState.Stopped)
        {
            return Succeed();
        }

        return Fail(RejectForCurrentState());
    }

    public RadarProcessingWorkerLifecycleResult MarkFaulted(
        RadarProcessingWorkerLifecycleError error = RadarProcessingWorkerLifecycleError.Faulted)
    {
        RadarProcessingWorkerGroupStatus.EnsureKnownLifecycleError(error);
        if (error == RadarProcessingWorkerLifecycleError.None)
        {
            throw new ArgumentOutOfRangeException(nameof(error), error, "Fault requires an explicit lifecycle error.");
        }

        if (state == RadarProcessingWorkerGroupState.Disposed)
        {
            return Fail(RadarProcessingWorkerLifecycleError.Disposed);
        }

        state = RadarProcessingWorkerGroupState.Faulted;
        health = RadarProcessingWorkerHealth.Faulted;
        lastError = error;
        return Succeed();
    }

    public RadarProcessingWorkerLifecycleResult Dispose()
    {
        if (state == RadarProcessingWorkerGroupState.Disposed)
        {
            return Succeed();
        }

        return TransitionTo(
            RadarProcessingWorkerGroupState.Disposed,
            RadarProcessingWorkerHealth.Disposed);
    }

    public RadarProcessingWorkerLifecycleResult ValidateDispatchAllowed()
    {
        if (state == RadarProcessingWorkerGroupState.Running &&
            health == RadarProcessingWorkerHealth.Healthy)
        {
            return Succeed();
        }

        return Fail(RejectForCurrentState());
    }

    private RadarProcessingWorkerLifecycleResult TransitionTo(
        RadarProcessingWorkerGroupState nextState,
        RadarProcessingWorkerHealth nextHealth)
    {
        state = nextState;
        health = nextHealth;
        lastError = RadarProcessingWorkerLifecycleError.None;
        return Succeed();
    }

    private RadarProcessingWorkerLifecycleResult Succeed() =>
        RadarProcessingWorkerLifecycleResult.Succeeded(Status);

    private RadarProcessingWorkerLifecycleResult Fail(
        RadarProcessingWorkerLifecycleError error)
    {
        lastError = error;
        return RadarProcessingWorkerLifecycleResult.Failed(error, Status);
    }

    private RadarProcessingWorkerLifecycleError RejectForCurrentState(
        bool starting = false) =>
        state switch
        {
            RadarProcessingWorkerGroupState.NotStarted => RadarProcessingWorkerLifecycleError.NotStarted,
            RadarProcessingWorkerGroupState.Running when starting => RadarProcessingWorkerLifecycleError.AlreadyStarted,
            RadarProcessingWorkerGroupState.Running => RadarProcessingWorkerLifecycleError.None,
            RadarProcessingWorkerGroupState.Stopping => RadarProcessingWorkerLifecycleError.Stopping,
            RadarProcessingWorkerGroupState.Stopped => RadarProcessingWorkerLifecycleError.Stopped,
            RadarProcessingWorkerGroupState.Faulted => RadarProcessingWorkerLifecycleError.Faulted,
            RadarProcessingWorkerGroupState.Disposed => RadarProcessingWorkerLifecycleError.Disposed,
            _ => RadarProcessingWorkerLifecycleError.NotRunning
        };
}
