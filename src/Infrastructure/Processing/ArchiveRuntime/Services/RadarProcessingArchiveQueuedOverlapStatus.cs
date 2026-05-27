namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Terminal status for archive producer and processing consumer overlap.
/// </summary>
public enum RadarProcessingArchiveQueuedOverlapStatus
{
    /// <summary>
    /// Overlap run has not started.
    /// </summary>
    NotStarted = 0,

    /// <summary>
    /// Producer and consumer both completed.
    /// </summary>
    Completed = 1,

    /// <summary>
    /// Producer failed while publishing archive batches.
    /// </summary>
    ProducerFailed = 2,

    /// <summary>
    /// Consumer faulted while draining queued batches.
    /// </summary>
    ConsumerFaulted = 3,

    /// <summary>
    /// Overlap run was canceled.
    /// </summary>
    Canceled = 4,

    /// <summary>
    /// Queue or consumer was disposed before completion.
    /// </summary>
    Disposed = 5
}
