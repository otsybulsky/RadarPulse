namespace RadarPulse.Domain.Processing;

/// <summary>
/// Reports whether async batch timeout enforcement fired and whether cancellation should be requested.
/// </summary>
public sealed record RadarProcessingAsyncTimeoutResult
{
    /// <summary>
    /// Gets the non-timeout result used when timeout enforcement is disabled or not triggered.
    /// </summary>
    public static RadarProcessingAsyncTimeoutResult None { get; } = new();

    /// <summary>
    /// Creates a timeout result and enforces consistency between timeout state, policy, and duration.
    /// </summary>
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

    /// <summary>
    /// Gets whether the batch exceeded its configured timeout.
    /// </summary>
    public bool TimedOut { get; }

    /// <summary>
    /// Gets the timeout threshold that was exceeded.
    /// </summary>
    public TimeSpan? Timeout { get; }

    /// <summary>
    /// Gets the timeout policy applied to the batch.
    /// </summary>
    public RadarProcessingWorkerTimeoutPolicy TimeoutPolicy { get; }

    /// <summary>
    /// Gets whether timeout handling should request cancellation for running work.
    /// </summary>
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
