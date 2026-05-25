namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingDurableRetryPolicy
{
    public static RadarProcessingDurableRetryPolicy NoRetry { get; } = new(maxAttempts: 1);

    public RadarProcessingDurableRetryPolicy(
        int maxAttempts,
        bool retryFailed = true,
        bool retryAbandoned = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        MaxAttempts = maxAttempts;
        RetryFailed = retryFailed;
        RetryAbandoned = retryAbandoned;
    }

    public int MaxAttempts { get; }

    public bool RetryFailed { get; }

    public bool RetryAbandoned { get; }

    public bool CanRetry(
        RadarProcessingDurableEnvelopeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.Attempt >= MaxAttempts)
        {
            return false;
        }

        return snapshot.State switch
        {
            RadarProcessingDurableEnvelopeState.Failed => RetryFailed,
            RadarProcessingDurableEnvelopeState.Abandoned => RetryAbandoned,
            _ => false
        };
    }
}
