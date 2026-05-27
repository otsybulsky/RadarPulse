namespace RadarPulse.Domain.Processing;

/// <summary>
/// Processing outcome for one dequeued provider batch.
/// </summary>
public enum RadarProcessingQueuedBatchProcessingStatus
{
    /// <summary>
    /// Processing completed successfully.
    /// </summary>
    Succeeded = 1,

    /// <summary>
    /// The processing core failed.
    /// </summary>
    FailedProcessing = 2,

    /// <summary>
    /// Output validation failed after processing.
    /// </summary>
    FailedValidation = 3,

    /// <summary>
    /// Rebalance or migration failed while handling the batch.
    /// </summary>
    FailedMigration = 4,

    /// <summary>
    /// Processing was canceled for this batch.
    /// </summary>
    Canceled = 5,

    /// <summary>
    /// The batch was skipped because an earlier batch faulted.
    /// </summary>
    SkippedAfterFault = 6
}
