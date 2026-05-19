namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingAsyncTimeoutResult
{
    public static RadarProcessingAsyncTimeoutResult None { get; } = new();

    public RadarProcessingAsyncTimeoutResult(
        bool timedOut = false,
        TimeSpan? timeout = null,
        RadarProcessingWorkerTimeoutPolicy timeoutPolicy = RadarProcessingWorkerTimeoutPolicy.Disabled,
        bool cancellationRequested = false)
    {
        EnsureKnownTimeoutPolicy(timeoutPolicy);

        if (!timedOut)
        {
            if (timeout.HasValue)
            {
                throw new ArgumentException(
                    "A non-timed-out result cannot carry a timeout duration.",
                    nameof(timeout));
            }

            if (timeoutPolicy != RadarProcessingWorkerTimeoutPolicy.Disabled)
            {
                throw new ArgumentException(
                    "A non-timed-out result cannot carry an active timeout policy.",
                    nameof(timeoutPolicy));
            }

            if (cancellationRequested)
            {
                throw new ArgumentException(
                    "A non-timed-out result cannot request cancellation.",
                    nameof(cancellationRequested));
            }
        }
        else if (!timeout.HasValue || timeout.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                "Timed-out results require a positive timeout duration.");
        }
        else if (timeoutPolicy == RadarProcessingWorkerTimeoutPolicy.Disabled)
        {
            throw new ArgumentException(
                "Timed-out results require a non-disabled timeout policy.",
                nameof(timeoutPolicy));
        }

        TimedOut = timedOut;
        Timeout = timeout;
        TimeoutPolicy = timeoutPolicy;
        CancellationRequested = cancellationRequested;
    }

    public bool TimedOut { get; }

    public TimeSpan? Timeout { get; }

    public RadarProcessingWorkerTimeoutPolicy TimeoutPolicy { get; }

    public bool CancellationRequested { get; }

    private static void EnsureKnownTimeoutPolicy(
        RadarProcessingWorkerTimeoutPolicy timeoutPolicy)
    {
        if (timeoutPolicy is not RadarProcessingWorkerTimeoutPolicy.Disabled and
            not RadarProcessingWorkerTimeoutPolicy.MarkUnhealthy and
            not RadarProcessingWorkerTimeoutPolicy.RequestCancellationAndMarkUnhealthy)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutPolicy));
        }
    }
}
