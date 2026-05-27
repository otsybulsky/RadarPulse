namespace RadarPulse.Domain.Processing;

public enum RadarProcessingQueuedBatchProcessingStatus
{
    Succeeded = 1,
    FailedProcessing = 2,
    FailedValidation = 3,
    FailedMigration = 4,
    Canceled = 5,
    SkippedAfterFault = 6
}
