using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncCompletionAggregatorTests
{
    [Fact]
    public void AsyncAggregationEnumValuesAreStable()
    {
        Assert.Equal(0, (int)RadarProcessingAsyncAggregationError.None);
        Assert.Equal(1, (int)RadarProcessingAsyncAggregationError.DispatchRejected);
        Assert.Equal(2, (int)RadarProcessingAsyncAggregationError.MissingBatchResult);
        Assert.Equal(3, (int)RadarProcessingAsyncAggregationError.IncompleteBatch);
        Assert.Equal(4, (int)RadarProcessingAsyncAggregationError.WorkFailed);
        Assert.Equal(5, (int)RadarProcessingAsyncAggregationError.WorkCanceled);
        Assert.Equal(6, (int)RadarProcessingAsyncAggregationError.CompletionCountMismatch);
        Assert.Equal(7, (int)RadarProcessingAsyncAggregationError.CompletionScopeMismatch);
        Assert.Equal(8, (int)RadarProcessingAsyncAggregationError.ProcessedStreamEventCountMismatch);
        Assert.Equal(9, (int)RadarProcessingAsyncAggregationError.ProcessedPayloadValueCountMismatch);
    }

    [Fact]
    public void OutOfOrderWorkerCompletionAggregatesInWorkItemOrder()
    {
        using var group = CreateStartedGroup(workerCount: 2, queueCapacity: 1);
        var topology = CreateTopology(sourceCount: 4, partitionCount: 4, shardCount: 2);
        var batch = CreateEightBitBatch(topology.SourceUniverseVersion, sourceIds: [0, 1, 2, 3]);
        var plan = new RadarProcessingAsyncBatchDispatcher(group, () => topology).CreatePlan(1, batch);
        var completions = new[]
        {
            CreateSucceededCompletion(plan, workItemId: 1),
            CreateSucceededCompletion(plan, workItemId: 0)
        };
        var dispatchResult = CreateDispatchResult(plan, CreateBatchResult(plan, completions));

        var aggregation = new RadarProcessingAsyncCompletionAggregator().Aggregate(dispatchResult);

        Assert.True(aggregation.IsSuccess);
        Assert.Equal(RadarProcessingAsyncAggregationError.None, aggregation.Error);
        Assert.NotNull(aggregation.Telemetry);
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, aggregation.Telemetry.ExecutionMode);
        Assert.Equal(topology.Version, aggregation.Telemetry.TopologyVersion);
        Assert.Equal(new[] { 0, 1 }, aggregation.OrderedCompletions.Select(static completion => completion.WorkItemId));
    }
}
