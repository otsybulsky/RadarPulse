namespace RadarPulse.Infrastructure.Processing;

public enum RadarProcessingAsyncAggregationError : byte
{
    None = 0,
    DispatchRejected = 1,
    MissingBatchResult = 2,
    IncompleteBatch = 3,
    WorkFailed = 4,
    WorkCanceled = 5,
    CompletionCountMismatch = 6,
    CompletionScopeMismatch = 7,
    ProcessedStreamEventCountMismatch = 8,
    ProcessedPayloadValueCountMismatch = 9
}
