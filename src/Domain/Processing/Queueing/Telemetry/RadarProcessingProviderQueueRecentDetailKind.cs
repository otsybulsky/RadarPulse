namespace RadarPulse.Domain.Processing;

/// <summary>
/// Category for one retained provider queue telemetry detail.
/// </summary>
public enum RadarProcessingProviderQueueRecentDetailKind
{
    /// <summary>
    /// Detail captured from an enqueue attempt.
    /// </summary>
    Enqueue = 1,

    /// <summary>
    /// Detail captured when a batch leaves the queue for processing.
    /// </summary>
    Dequeue = 2,

    /// <summary>
    /// Detail captured from batch processing completion.
    /// </summary>
    Processing = 3
}
