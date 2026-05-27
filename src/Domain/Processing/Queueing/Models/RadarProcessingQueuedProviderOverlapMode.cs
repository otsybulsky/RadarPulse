namespace RadarPulse.Domain.Processing;

/// <summary>
/// Degree of producer/consumer overlap allowed by a queued provider run.
/// </summary>
public enum RadarProcessingQueuedProviderOverlapMode
{
    /// <summary>
    /// Producer and processing work are not overlapped.
    /// </summary>
    None = 0,

    /// <summary>
    /// Producer intake and processing run concurrently through the provider queue.
    /// </summary>
    ProducerConsumer = 1
}
