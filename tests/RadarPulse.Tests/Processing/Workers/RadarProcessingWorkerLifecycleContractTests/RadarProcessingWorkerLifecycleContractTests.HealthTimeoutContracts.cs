using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingWorkerLifecycleContractTests
{
    [Fact]
    public void HealthTransitionAndTimeoutResultContractsAreExplicit()
    {
        var previous = new RadarProcessingWorkerGroupStatus(
            RadarProcessingWorkerGroupState.Running,
            RadarProcessingWorkerHealth.Healthy,
            workerCount: 1,
            queueCapacity: 1);
        var current = new RadarProcessingWorkerGroupStatus(
            RadarProcessingWorkerGroupState.Faulted,
            RadarProcessingWorkerHealth.Faulted,
            workerCount: 1,
            queueCapacity: 1,
            lastError: RadarProcessingWorkerLifecycleError.Faulted);

        var transition = new RadarProcessingWorkerGroupHealthTransition(
            previous,
            current,
            RadarProcessingAsyncFailureKind.TimedOut);
        var timeout = new RadarProcessingAsyncTimeoutResult(
            timedOut: true,
            timeout: TimeSpan.FromMilliseconds(25),
            timeoutPolicy: RadarProcessingWorkerTimeoutPolicy.RequestCancellationAndMarkUnhealthy,
            cancellationRequested: true);

        Assert.True(transition.Changed);
        Assert.Same(previous, transition.PreviousStatus);
        Assert.Same(current, transition.CurrentStatus);
        Assert.Equal(RadarProcessingAsyncFailureKind.TimedOut, transition.FailureKind);
        Assert.True(timeout.TimedOut);
        Assert.Equal(TimeSpan.FromMilliseconds(25), timeout.Timeout);
        Assert.True(timeout.CancellationRequested);
        Assert.False(RadarProcessingAsyncTimeoutResult.None.TimedOut);
        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingWorkerGroupHealthTransition(null!, current, RadarProcessingAsyncFailureKind.TimedOut));
        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingWorkerGroupHealthTransition(previous, null!, RadarProcessingAsyncFailureKind.TimedOut));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingWorkerGroupHealthTransition(previous, current, RadarProcessingAsyncFailureKind.None));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingWorkerGroupHealthTransition(previous, current, (RadarProcessingAsyncFailureKind)255));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingAsyncTimeoutResult(timeout: TimeSpan.FromMilliseconds(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncTimeoutResult(
                timedOut: true,
                timeout: TimeSpan.Zero,
                timeoutPolicy: RadarProcessingWorkerTimeoutPolicy.MarkUnhealthy));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingAsyncTimeoutResult(
                timedOut: true,
                timeout: TimeSpan.FromMilliseconds(1),
                timeoutPolicy: RadarProcessingWorkerTimeoutPolicy.Disabled));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncTimeoutResult(
                timedOut: true,
                timeout: TimeSpan.FromMilliseconds(1),
                timeoutPolicy: (RadarProcessingWorkerTimeoutPolicy)255));
    }
}
