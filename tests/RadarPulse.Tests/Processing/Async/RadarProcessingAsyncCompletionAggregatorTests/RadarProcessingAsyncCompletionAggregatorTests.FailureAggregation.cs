using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncCompletionAggregatorTests
{
    [Fact]
    public void MissingCompletionFailsAggregationWithoutSuccessfulTelemetry()
    {
        using var group = CreateStartedGroup(workerCount: 2, queueCapacity: 1);
        var plan = CreatePlan(group);
        var scope = new RadarProcessingAsyncBatchScope(
            plan.BatchSequence,
            plan.TopologyVersion,
            plan.ExpectedWorkItemCount);
        Assert.True(scope.RecordCompletion(CreateSucceededCompletion(plan, workItemId: 0)).IsSuccess);
        var dispatchResult = CreateDispatchResult(plan, scope.Complete());

        var aggregation = new RadarProcessingAsyncCompletionAggregator().Aggregate(dispatchResult);
        var result = aggregation.CreateProcessingResult(RadarProcessingMetrics.Empty);

        Assert.False(aggregation.IsSuccess);
        Assert.Equal(RadarProcessingAsyncAggregationError.IncompleteBatch, aggregation.Error);
        Assert.Null(aggregation.Telemetry);
        Assert.False(result.IsValid);
        Assert.Null(result.Telemetry);
    }

    [Fact]
    public void DuplicateCompletionFailsAggregationWithoutSuccessfulTelemetry()
    {
        using var group = CreateStartedGroup(workerCount: 2, queueCapacity: 1);
        var plan = CreatePlan(group);
        var scope = new RadarProcessingAsyncBatchScope(
            plan.BatchSequence,
            plan.TopologyVersion,
            plan.ExpectedWorkItemCount);
        var completion = CreateSucceededCompletion(plan, workItemId: 0);

        Assert.True(scope.RecordCompletion(completion).IsSuccess);
        var duplicate = scope.RecordCompletion(completion);
        var dispatchResult = CreateDispatchResult(plan, duplicate);

        var aggregation = new RadarProcessingAsyncCompletionAggregator().Aggregate(dispatchResult);

        Assert.False(aggregation.IsSuccess);
        Assert.Equal(RadarProcessingAsyncAggregationError.CompletionScopeMismatch, aggregation.Error);
        Assert.Null(aggregation.Telemetry);
    }

    [Fact]
    public void FailedWorkerCompletionPreventsSuccessfulTelemetryProjection()
    {
        using var group = CreateStartedGroup(workerCount: 2, queueCapacity: 1);
        var plan = CreatePlan(group);
        var completions = new[]
        {
            RadarProcessingAsyncWorkCompletion.Failed(plan.WorkItems[0]),
            CreateSucceededCompletion(plan, workItemId: 1)
        };
        var dispatchResult = CreateDispatchResult(
            plan,
            CreateBatchResult(plan, completions, RadarProcessingAsyncBatchCompletionError.WorkFailed));

        var aggregation = new RadarProcessingAsyncCompletionAggregator().Aggregate(dispatchResult);

        Assert.False(aggregation.IsSuccess);
        Assert.Equal(RadarProcessingAsyncAggregationError.WorkFailed, aggregation.Error);
        Assert.Null(aggregation.Telemetry);
    }

    [Fact]
    public void TimedOutDispatchPreventsSuccessfulTelemetryProjection()
    {
        using var group = CreateStartedGroup(workerCount: 2, queueCapacity: 1);
        var plan = CreatePlan(group);
        var batchResult = CreateBatchResult(
            plan,
            new[]
            {
                CreateSucceededCompletion(plan, workItemId: 0),
                CreateSucceededCompletion(plan, workItemId: 1)
            });
        var workerResult = RadarProcessingAsyncWorkerGroupResult.Rejected(
            new RadarProcessingWorkerGroupStatus(
                RadarProcessingWorkerGroupState.Faulted,
                RadarProcessingWorkerHealth.Faulted,
                workerCount: 2,
                queueCapacity: 1,
                lastError: RadarProcessingWorkerLifecycleError.Faulted),
            RadarProcessingAsyncWorkerGroupError.TimedOut,
            batchResult,
            new RadarProcessingAsyncWorkerGroupDrainResult(
                acceptedWorkItemCount: batchResult.Completion.RecordedWorkItemCount,
                completedWorkItemCount: batchResult.Completion.RecordedWorkItemCount,
                timedOut: true),
            RadarProcessingAsyncFailureKind.TimedOut,
            timeoutResult: new RadarProcessingAsyncTimeoutResult(
                timedOut: true,
                timeout: TimeSpan.FromMilliseconds(25),
                timeoutPolicy: RadarProcessingWorkerTimeoutPolicy.MarkUnhealthy));
        var dispatchResult = new RadarProcessingAsyncDispatchResult(plan, workerResult);

        var aggregation = new RadarProcessingAsyncCompletionAggregator().Aggregate(dispatchResult);

        Assert.False(aggregation.IsSuccess);
        Assert.Equal(RadarProcessingAsyncAggregationError.DispatchRejected, aggregation.Error);
        Assert.Null(aggregation.Telemetry);
        Assert.False(aggregation.CreateProcessingResult(RadarProcessingMetrics.Empty).IsValid);
    }

    [Fact]
    public void CompletionMetricMismatchPreventsSuccessfulTelemetryProjection()
    {
        using var group = CreateStartedGroup(workerCount: 2, queueCapacity: 1);
        var plan = CreatePlan(group);
        var first = CreateSucceededCompletion(plan, workItemId: 0);
        var secondWorkItem = plan.WorkItems[1];
        var second = RadarProcessingAsyncWorkCompletion.Succeeded(
            secondWorkItem,
            processedStreamEventCount: 0,
            processedPayloadValueCount: plan.Route.GetShard(secondWorkItem.ShardId).Metrics.PayloadValueCount);
        var dispatchResult = CreateDispatchResult(plan, CreateBatchResult(plan, new[] { first, second }));

        var aggregation = new RadarProcessingAsyncCompletionAggregator().Aggregate(dispatchResult);

        Assert.False(aggregation.IsSuccess);
        Assert.Equal(RadarProcessingAsyncAggregationError.ProcessedStreamEventCountMismatch, aggregation.Error);
        Assert.Null(aggregation.Telemetry);
    }
}
