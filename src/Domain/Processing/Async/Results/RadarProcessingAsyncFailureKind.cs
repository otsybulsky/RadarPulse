namespace RadarPulse.Domain.Processing;

/// <summary>
/// Classifies async processing failures for completion and worker telemetry.
/// </summary>
public enum RadarProcessingAsyncFailureKind : byte
{
    /// <summary>
    /// No failure was observed.
    /// </summary>
    None = 0,

    /// <summary>
    /// The worker returned a domain failure result.
    /// </summary>
    WorkerReportedFailure = 1,

    /// <summary>
    /// The worker threw while running the work item.
    /// </summary>
    WorkerException = 2,

    /// <summary>
    /// Dispatch was rejected before the item reached a worker queue.
    /// </summary>
    DispatchRejected = 3,

    /// <summary>
    /// Enqueue into a worker queue was rejected.
    /// </summary>
    EnqueueRejected = 4,

    /// <summary>
    /// Timeout enforcement classified the work as failed.
    /// </summary>
    TimedOut = 5,

    /// <summary>
    /// The worker group was faulted and could not complete dispatch.
    /// </summary>
    WorkerGroupFaulted = 6
}
