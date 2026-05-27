namespace RadarPulse.Domain.Processing;

public enum RadarProcessingAsyncBatchCompletionError : byte
{
    None = 0,
    ScopeMismatch = 1,
    TopologyVersionMismatch = 2,
    WorkItemOutOfRange = 3,
    DuplicateCompletion = 4,
    MissingCompletion = 5,
    ScopeClosed = 6,
    WorkFailed = 7,
    WorkCanceled = 8
}
