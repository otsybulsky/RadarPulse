namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Archive producer status for queued-overlap runs.
/// </summary>
public enum RadarProcessingArchiveQueuedOverlapProducerStatus
{
    /// <summary>
    /// Producer has not started.
    /// </summary>
    NotStarted = 0,

    /// <summary>
    /// Producer published all archive batches.
    /// </summary>
    Completed = 1,

    /// <summary>
    /// Producer failed while publishing.
    /// </summary>
    Failed = 2,

    /// <summary>
    /// Producer observed cancellation.
    /// </summary>
    Canceled = 3
}
