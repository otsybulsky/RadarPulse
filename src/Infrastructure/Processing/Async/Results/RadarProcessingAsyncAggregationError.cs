namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Validation errors produced while aggregating async worker completions.
/// </summary>
public enum RadarProcessingAsyncAggregationError : byte
{
    /// <summary>
    /// Aggregation succeeded.
    /// </summary>
    None = 0,

    /// <summary>
    /// Worker group dispatch was rejected before successful completion.
    /// </summary>
    DispatchRejected = 1,

    /// <summary>
    /// Worker group dispatch did not provide a batch result.
    /// </summary>
    MissingBatchResult = 2,

    /// <summary>
    /// The batch did not record every expected work item completion.
    /// </summary>
    IncompleteBatch = 3,

    /// <summary>
    /// At least one work item reported failure.
    /// </summary>
    WorkFailed = 4,

    /// <summary>
    /// At least one work item reported cancellation.
    /// </summary>
    WorkCanceled = 5,

    /// <summary>
    /// Completion count or work item ids did not match the dispatch plan.
    /// </summary>
    CompletionCountMismatch = 6,

    /// <summary>
    /// Completion batch sequence, topology version, or worker assignment did not match the plan.
    /// </summary>
    CompletionScopeMismatch = 7,

    /// <summary>
    /// Aggregated stream event count did not match the route.
    /// </summary>
    ProcessedStreamEventCountMismatch = 8,

    /// <summary>
    /// Aggregated payload value count did not match the route.
    /// </summary>
    ProcessedPayloadValueCountMismatch = 9
}
