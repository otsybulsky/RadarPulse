namespace RadarPulse.Domain.Processing;

/// <summary>
/// Retry policy for failed or abandoned durable envelopes.
/// </summary>
public sealed record RadarProcessingDurableRetryPolicy
{
    /// <summary>
    /// Policy that prevents automatic retry after the first attempt.
    /// </summary>
    public static RadarProcessingDurableRetryPolicy NoRetry { get; } = new(maxAttempts: 1);

    /// <summary>
    /// Creates a retry policy with a maximum attempt count and retryable states.
    /// </summary>
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

    /// <summary>
    /// Maximum processing attempts allowed for an envelope.
    /// </summary>
    public int MaxAttempts { get; }

    /// <summary>
    /// Indicates whether failed envelopes may be retried.
    /// </summary>
    public bool RetryFailed { get; }

    /// <summary>
    /// Indicates whether abandoned envelopes may be retried.
    /// </summary>
    public bool RetryAbandoned { get; }

    /// <summary>
    /// Returns whether the supplied snapshot is retryable under this policy.
    /// </summary>
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
