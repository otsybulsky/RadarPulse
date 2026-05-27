using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Validates async worker completions and converts them into processing telemetry.
/// </summary>
public sealed class RadarProcessingAsyncCompletionAggregator
{
    /// <summary>
    /// Aggregates a dispatch result into ordered completions and route-level telemetry.
    /// </summary>
    public RadarProcessingAsyncAggregationResult Aggregate(
        RadarProcessingAsyncDispatchResult dispatchResult)
    {
        ArgumentNullException.ThrowIfNull(dispatchResult);

        var batchResult = dispatchResult.BatchResult;
        if (dispatchResult.WorkerGroupResult.IsRejected)
        {
            return Failed(
                dispatchResult,
                RadarProcessingAsyncAggregationError.DispatchRejected,
                batchResult);
        }

        if (batchResult is null)
        {
            return Failed(
                dispatchResult,
                RadarProcessingAsyncAggregationError.MissingBatchResult);
        }

        if (!batchResult.IsSuccess)
        {
            return Failed(
                dispatchResult,
                MapBatchError(batchResult.Error),
                batchResult);
        }

        var completion = batchResult.Completion;
        if (!completion.IsComplete ||
            completion.RecordedWorkItemCount != dispatchResult.Plan.ExpectedWorkItemCount)
        {
            return Failed(
                dispatchResult,
                RadarProcessingAsyncAggregationError.IncompleteBatch,
                batchResult);
        }

        var ordered = new RadarProcessingAsyncWorkCompletion[dispatchResult.Plan.ExpectedWorkItemCount];
        var expectedWorkItems = CreateExpectedWorkItemMap(dispatchResult.Plan);
        var processedStreamEventCount = 0L;
        var processedPayloadValueCount = 0L;

        foreach (var workCompletion in completion.Completions)
        {
            var validationError = ValidateCompletionScope(
                dispatchResult.Plan,
                expectedWorkItems,
                ordered,
                workCompletion);
            if (validationError != RadarProcessingAsyncAggregationError.None)
            {
                return Failed(dispatchResult, validationError, batchResult);
            }

            if (workCompletion.IsFailed)
            {
                return Failed(dispatchResult, RadarProcessingAsyncAggregationError.WorkFailed, batchResult);
            }

            if (workCompletion.IsCanceled)
            {
                return Failed(dispatchResult, RadarProcessingAsyncAggregationError.WorkCanceled, batchResult);
            }

            var expectedWorkItem = expectedWorkItems[workCompletion.WorkItemId];
            var expectedShard = dispatchResult.Route.GetShard(expectedWorkItem.ShardId);
            if (workCompletion.ProcessedStreamEventCount != expectedShard.Metrics.EventCount)
            {
                return Failed(
                    dispatchResult,
                    RadarProcessingAsyncAggregationError.ProcessedStreamEventCountMismatch,
                    batchResult);
            }

            if (workCompletion.ProcessedPayloadValueCount != expectedShard.Metrics.PayloadValueCount)
            {
                return Failed(
                    dispatchResult,
                    RadarProcessingAsyncAggregationError.ProcessedPayloadValueCountMismatch,
                    batchResult);
            }

            ordered[workCompletion.WorkItemId] = workCompletion;
            processedStreamEventCount = checked(
                processedStreamEventCount + workCompletion.ProcessedStreamEventCount);
            processedPayloadValueCount = checked(
                processedPayloadValueCount + workCompletion.ProcessedPayloadValueCount);
        }

        if (ordered.Any(static item => item is null))
        {
            return Failed(
                dispatchResult,
                RadarProcessingAsyncAggregationError.CompletionCountMismatch,
                batchResult);
        }

        if (processedStreamEventCount != dispatchResult.Route.Metrics.EventCount)
        {
            return Failed(
                dispatchResult,
                RadarProcessingAsyncAggregationError.ProcessedStreamEventCountMismatch,
                batchResult);
        }

        if (processedPayloadValueCount != dispatchResult.Route.Metrics.PayloadValueCount)
        {
            return Failed(
                dispatchResult,
                RadarProcessingAsyncAggregationError.ProcessedPayloadValueCountMismatch,
                batchResult);
        }

        var telemetry = RadarProcessingTelemetry.FromRoute(
            RadarProcessingExecutionMode.AsyncShardTransport,
            dispatchResult.Route);
        return new RadarProcessingAsyncAggregationResult(
            dispatchResult,
            telemetry: telemetry,
            orderedCompletions: ordered!);
    }

    private static RadarProcessingAsyncWorkItem[] CreateExpectedWorkItemMap(
        RadarProcessingAsyncDispatchPlan plan)
    {
        var result = new RadarProcessingAsyncWorkItem[plan.ExpectedWorkItemCount];
        foreach (var workItem in plan.WorkItems)
        {
            if ((uint)workItem.WorkItemId >= (uint)result.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(plan));
            }

            result[workItem.WorkItemId] = workItem;
        }

        return result;
    }

    private static RadarProcessingAsyncAggregationError ValidateCompletionScope(
        RadarProcessingAsyncDispatchPlan plan,
        IReadOnlyList<RadarProcessingAsyncWorkItem> expectedWorkItems,
        IReadOnlyList<RadarProcessingAsyncWorkCompletion?> ordered,
        RadarProcessingAsyncWorkCompletion completion)
    {
        if (completion.BatchSequence != plan.BatchSequence ||
            completion.TopologyVersion != plan.TopologyVersion)
        {
            return RadarProcessingAsyncAggregationError.CompletionScopeMismatch;
        }

        if ((uint)completion.WorkItemId >= (uint)expectedWorkItems.Count ||
            ordered[completion.WorkItemId] is not null)
        {
            return RadarProcessingAsyncAggregationError.CompletionCountMismatch;
        }

        var expected = expectedWorkItems[completion.WorkItemId];
        if (expected is null ||
            expected.WorkerId != completion.WorkerId)
        {
            return RadarProcessingAsyncAggregationError.CompletionScopeMismatch;
        }

        return RadarProcessingAsyncAggregationError.None;
    }

    private static RadarProcessingAsyncAggregationResult Failed(
        RadarProcessingAsyncDispatchResult dispatchResult,
        RadarProcessingAsyncAggregationError error,
        RadarProcessingAsyncBatchScopeResult? batchResult = null) =>
        new(
            dispatchResult,
            error,
            orderedCompletions: batchResult?.Completion.Completions ?? Array.Empty<RadarProcessingAsyncWorkCompletion>());

    private static RadarProcessingAsyncAggregationError MapBatchError(
        RadarProcessingAsyncBatchCompletionError batchError) =>
        batchError switch
        {
            RadarProcessingAsyncBatchCompletionError.WorkFailed => RadarProcessingAsyncAggregationError.WorkFailed,
            RadarProcessingAsyncBatchCompletionError.WorkCanceled => RadarProcessingAsyncAggregationError.WorkCanceled,
            RadarProcessingAsyncBatchCompletionError.MissingCompletion => RadarProcessingAsyncAggregationError.IncompleteBatch,
            RadarProcessingAsyncBatchCompletionError.None => RadarProcessingAsyncAggregationError.None,
            _ => RadarProcessingAsyncAggregationError.CompletionScopeMismatch
        };
}
