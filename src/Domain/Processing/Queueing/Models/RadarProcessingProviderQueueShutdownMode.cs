namespace RadarPulse.Domain.Processing;

/// <summary>
/// Shutdown behavior for accepted work remaining in a provider queue.
/// </summary>
public enum RadarProcessingProviderQueueShutdownMode
{
    /// <summary>
    /// Drain accepted queued work before terminal shutdown.
    /// </summary>
    Drain = 1,

    /// <summary>
    /// Cancel queued work that has not been processed.
    /// </summary>
    CancelQueued = 2
}
