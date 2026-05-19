using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingAsyncAggregationResult
{
    private readonly IReadOnlyList<RadarProcessingAsyncWorkCompletion> orderedCompletions;

    public RadarProcessingAsyncAggregationResult(
        RadarProcessingAsyncDispatchResult dispatchResult,
        RadarProcessingAsyncAggregationError error = RadarProcessingAsyncAggregationError.None,
        RadarProcessingTelemetry? telemetry = null,
        IReadOnlyCollection<RadarProcessingAsyncWorkCompletion>? orderedCompletions = null)
    {
        ArgumentNullException.ThrowIfNull(dispatchResult);
        EnsureKnownError(error);

        if (error == RadarProcessingAsyncAggregationError.None && telemetry is null)
        {
            throw new ArgumentException("Successful async aggregation requires telemetry.", nameof(telemetry));
        }

        if (error != RadarProcessingAsyncAggregationError.None && telemetry is not null)
        {
            throw new ArgumentException("Failed async aggregation cannot carry successful telemetry.", nameof(telemetry));
        }

        DispatchResult = dispatchResult;
        Error = error;
        Telemetry = telemetry;
        this.orderedCompletions = CopyCompletions(orderedCompletions ?? Array.Empty<RadarProcessingAsyncWorkCompletion>());
    }

    public RadarProcessingAsyncDispatchResult DispatchResult { get; }

    public RadarProcessingAsyncAggregationError Error { get; }

    public RadarProcessingTelemetry? Telemetry { get; }

    public IReadOnlyList<RadarProcessingAsyncWorkCompletion> OrderedCompletions => orderedCompletions;

    public bool IsSuccess => Error == RadarProcessingAsyncAggregationError.None;

    public RadarProcessingResult CreateProcessingResult(
        RadarProcessingMetrics metrics) =>
        new(
            RadarProcessingExecutionMode.AsyncShardTransport,
            DispatchResult.Plan.PartitionCount,
            DispatchResult.Plan.ShardCount,
            metrics,
            IsSuccess
                ? RadarProcessingValidationResult.Valid(metrics)
                : RadarProcessingValidationResult.Invalid(
                    MapValidationError(Error),
                    sourceId: -1,
                    eventIndex: -1,
                    CreateFailureMessage(Error),
                    metrics),
            Telemetry,
            DispatchResult.TopologyVersion);

    internal static void EnsureKnownError(
        RadarProcessingAsyncAggregationError error)
    {
        if (error is not RadarProcessingAsyncAggregationError.None and
            not RadarProcessingAsyncAggregationError.DispatchRejected and
            not RadarProcessingAsyncAggregationError.MissingBatchResult and
            not RadarProcessingAsyncAggregationError.IncompleteBatch and
            not RadarProcessingAsyncAggregationError.WorkFailed and
            not RadarProcessingAsyncAggregationError.WorkCanceled and
            not RadarProcessingAsyncAggregationError.CompletionCountMismatch and
            not RadarProcessingAsyncAggregationError.CompletionScopeMismatch and
            not RadarProcessingAsyncAggregationError.ProcessedStreamEventCountMismatch and
            not RadarProcessingAsyncAggregationError.ProcessedPayloadValueCountMismatch)
        {
            throw new ArgumentOutOfRangeException(nameof(error));
        }
    }

    private static IReadOnlyList<RadarProcessingAsyncWorkCompletion> CopyCompletions(
        IReadOnlyCollection<RadarProcessingAsyncWorkCompletion> completions)
    {
        var result = new RadarProcessingAsyncWorkCompletion[completions.Count];
        var index = 0;
        foreach (var completion in completions)
        {
            ArgumentNullException.ThrowIfNull(completion);
            result[index++] = completion;
        }

        return Array.AsReadOnly(result);
    }

    private static RadarProcessingValidationError MapValidationError(
        RadarProcessingAsyncAggregationError error) =>
        error == RadarProcessingAsyncAggregationError.WorkCanceled
            ? RadarProcessingValidationError.Canceled
            : RadarProcessingValidationError.MetricsMismatch;

    private static string CreateFailureMessage(
        RadarProcessingAsyncAggregationError error) =>
        error switch
        {
            RadarProcessingAsyncAggregationError.DispatchRejected => "Async dispatch was rejected before successful aggregation.",
            RadarProcessingAsyncAggregationError.MissingBatchResult => "Async dispatch did not produce a batch result.",
            RadarProcessingAsyncAggregationError.IncompleteBatch => "Async dispatch batch completion is incomplete.",
            RadarProcessingAsyncAggregationError.WorkFailed => "Async dispatch contains failed work.",
            RadarProcessingAsyncAggregationError.WorkCanceled => "Async dispatch contains canceled work.",
            RadarProcessingAsyncAggregationError.CompletionCountMismatch => "Async completion count does not match the dispatch plan.",
            RadarProcessingAsyncAggregationError.CompletionScopeMismatch => "Async completion scope does not match the dispatch plan.",
            RadarProcessingAsyncAggregationError.ProcessedStreamEventCountMismatch => "Async completion event count does not match the route.",
            RadarProcessingAsyncAggregationError.ProcessedPayloadValueCountMismatch => "Async completion payload value count does not match the route.",
            _ => throw new ArgumentOutOfRangeException(nameof(error))
        };
}
