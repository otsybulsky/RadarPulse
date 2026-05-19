using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingAsyncCompletionAggregatorTests
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

    [Fact]
    public async Task AsyncAggregationCanProduceResultComparableWithSynchronousPartitionedOutput()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var syncCore = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount: 4,
                shardCount: 2));
        var asyncStore = new RadarSourceProcessingStateStore(universe);
        var beforeSnapshots = asyncStore.CreateSnapshots();
        var previousMetrics = asyncStore.CreateMetrics();
        await using var group = CreateStartedGroup(workerCount: 1, queueCapacity: 2);
        var dispatcher = new RadarProcessingAsyncBatchDispatcher(group, () => syncCore.Topology);
        var batch = CreateEightBitBatch(universe.Version, sourceIds: [0, 1, 2, 3]);

        var syncResult = syncCore.Process(batch);
        var dispatchResult = await dispatcher.DispatchAsync(
            batchSequence: 1,
            batch,
            (borrowedBatch, route, workItem, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var shard = route.GetShard(workItem.ShardId);
                foreach (var eventIndex in shard.EventIndexes.Span)
                {
                    var streamEvent = borrowedBatch.Events.Span[eventIndex];
                    var payloadMetrics = route.GetRoutedEvent(eventIndex).PayloadMetrics;
                    asyncStore.ApplyProcessedEvent(
                        streamEvent,
                        payloadMetrics.PayloadValueCount,
                        payloadMetrics.RawValueChecksum);
                }

                return ValueTask.FromResult(
                    RadarProcessingAsyncWorkCompletion.Succeeded(
                        workItem,
                        processedStreamEventCount: shard.EventIndexes.Length,
                        processedPayloadValueCount: shard.Metrics.PayloadValueCount));
            });

        var aggregation = new RadarProcessingAsyncCompletionAggregator().Aggregate(dispatchResult);
        var asyncResult = aggregation.CreateProcessingResult(asyncStore.CreateMetrics(processedBatchCount: 1));
        var validation = RadarProcessingOutputValidator.Validate(
            batch,
            asyncResult,
            beforeSnapshots,
            asyncStore.CreateSnapshots(),
            previousMetrics);

        Assert.True(aggregation.IsSuccess);
        Assert.True(asyncResult.IsValid);
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, asyncResult.ExecutionMode);
        Assert.Equal(syncResult.Metrics, asyncResult.Metrics);
        Assert.NotNull(asyncResult.Telemetry);
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, asyncResult.Telemetry.ExecutionMode);
        Assert.Equal(syncResult.Telemetry?.BatchMetrics, asyncResult.Telemetry.BatchMetrics);
        Assert.True(validation.IsValid);
    }

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

    [Fact]
    public void AggregationContractsRejectInvalidShapes()
    {
        using var group = CreateStartedGroup(workerCount: 2, queueCapacity: 1);
        var plan = CreatePlan(group);
        var dispatchResult = CreateDispatchResult(
            plan,
            CreateBatchResult(
                plan,
                new[]
                {
                    CreateSucceededCompletion(plan, workItemId: 0),
                    CreateSucceededCompletion(plan, workItemId: 1)
                }));
        var telemetry = RadarProcessingTelemetry.FromRoute(
            RadarProcessingExecutionMode.AsyncShardTransport,
            plan.Route);

        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingAsyncCompletionAggregator().Aggregate(null!));
        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingAsyncAggregationResult(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncAggregationResult(dispatchResult, (RadarProcessingAsyncAggregationError)255));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingAsyncAggregationResult(dispatchResult));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingAsyncAggregationResult(
                dispatchResult,
                RadarProcessingAsyncAggregationError.WorkFailed,
                telemetry));
    }

    private static RadarProcessingAsyncDispatchPlan CreatePlan(
        RadarProcessingAsyncWorkerGroup group)
    {
        var topology = CreateTopology(sourceCount: 4, partitionCount: 4, shardCount: 2);
        var batch = CreateEightBitBatch(topology.SourceUniverseVersion, sourceIds: [0, 1, 2, 3]);
        return new RadarProcessingAsyncBatchDispatcher(group, () => topology).CreatePlan(1, batch);
    }

    private static RadarProcessingAsyncWorkCompletion CreateSucceededCompletion(
        RadarProcessingAsyncDispatchPlan plan,
        int workItemId)
    {
        var workItem = plan.WorkItems[workItemId];
        var shard = plan.Route.GetShard(workItem.ShardId);
        return RadarProcessingAsyncWorkCompletion.Succeeded(
            workItem,
            processedStreamEventCount: shard.EventIndexes.Length,
            processedPayloadValueCount: shard.Metrics.PayloadValueCount);
    }

    private static RadarProcessingAsyncBatchScopeResult CreateBatchResult(
        RadarProcessingAsyncDispatchPlan plan,
        IReadOnlyCollection<RadarProcessingAsyncWorkCompletion> completions,
        RadarProcessingAsyncBatchCompletionError error = RadarProcessingAsyncBatchCompletionError.None) =>
        new(
            new RadarProcessingAsyncBatchCompletion(
                plan.BatchSequence,
                plan.TopologyVersion,
                plan.ExpectedWorkItemCount,
                completions,
                isClosed: true),
            error);

    private static RadarProcessingAsyncDispatchResult CreateDispatchResult(
        RadarProcessingAsyncDispatchPlan plan,
        RadarProcessingAsyncBatchScopeResult batchResult)
    {
        var status = new RadarProcessingWorkerGroupStatus(
            RadarProcessingWorkerGroupState.Running,
            RadarProcessingWorkerHealth.Healthy,
            plan.ShardCount,
            queueCapacity: 1);
        var workerResult = RadarProcessingAsyncWorkerGroupResult.Completed(
            status,
            batchResult,
            new RadarProcessingAsyncWorkerGroupDrainResult(
                acceptedWorkItemCount: batchResult.Completion.RecordedWorkItemCount,
                completedWorkItemCount: batchResult.Completion.RecordedWorkItemCount));
        return new RadarProcessingAsyncDispatchResult(plan, workerResult);
    }

    private static RadarProcessingAsyncWorkerGroup CreateStartedGroup(
        int workerCount,
        int queueCapacity)
    {
        var group = new RadarProcessingAsyncWorkerGroup(
            new RadarProcessingAsyncWorkerGroupOptions(
                new RadarProcessingAsyncExecutionOptions(workerCount: workerCount, queueCapacity: queueCapacity)));
        Assert.True(group.Start().IsSuccess);
        return group;
    }

    private static RadarProcessingTopology CreateTopology(
        int sourceCount,
        int partitionCount,
        int shardCount) =>
        new(
            CreateUniverse(sourceCount),
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount,
                shardCount));

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarEventBatch CreateEightBitBatch(
        SourceUniverseVersion sourceUniverseVersion,
        int[] sourceIds)
    {
        var events = new RadarStreamEvent[sourceIds.Length];
        var payload = new byte[sourceIds.Length];
        for (var i = 0; i < sourceIds.Length; i++)
        {
            events[i] = CreateEvent(
                sourceIds[i],
                payloadOffset: i,
                gateCount: 1,
                wordSize: RadarStreamWordSize.EightBit);
            payload[i] = (byte)(i + 1);
        }

        return new RadarEventBatch(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            events,
            payload);
    }

    private static RadarStreamEvent CreateEvent(
        int sourceId,
        int payloadOffset,
        ushort gateCount,
        RadarStreamWordSize wordSize)
    {
        var payloadLength = checked(gateCount * (wordSize == RadarStreamWordSize.EightBit ? 1 : 2));
        return new RadarStreamEvent(
            sourceId,
            radarOrdinal: 0,
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks: 100 + payloadOffset,
            sourceRecord: 1,
            sourceMessage: 1,
            radialSequence: payloadOffset,
            elevationSlot: 0,
            azimuthBucket: (ushort)sourceId,
            rangeBand: 0,
            momentId: 0,
            gateStart: 0,
            gateCount: gateCount,
            wordSize: wordSize,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payloadOffset: payloadOffset,
            payloadLength: payloadLength);
    }
}
