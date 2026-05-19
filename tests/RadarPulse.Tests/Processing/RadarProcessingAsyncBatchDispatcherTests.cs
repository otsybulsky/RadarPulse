using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingAsyncBatchDispatcherTests
{
    [Fact]
    public void DispatcherCreatesOneShardWorkItemPlanAgainstCapturedTopology()
    {
        using var group = CreateStartedGroup(workerCount: 2, queueCapacity: 2);
        var topology = CreateTopology(sourceCount: 6, partitionCount: 6, shardCount: 3);
        var providerCallCount = 0;
        var dispatcher = new RadarProcessingAsyncBatchDispatcher(
            group,
            () =>
            {
                providerCallCount++;
                return topology;
            });
        var batch = CreateEightBitBatch(topology.SourceUniverseVersion, sourceIds: [0, 1, 3, 5]);

        var plan = dispatcher.CreatePlan(batchSequence: 17, batch);

        Assert.Equal(1, providerCallCount);
        Assert.Equal(17, plan.BatchSequence);
        Assert.Equal(topology.Version, plan.TopologyVersion);
        Assert.Equal(topology.ShardCount, plan.ExpectedWorkItemCount);
        Assert.Equal(topology.PartitionCount, plan.PartitionCount);
        Assert.Equal(topology.ShardCount, plan.ShardCount);
        Assert.Equal(batch.EventCount, plan.RoutedEventCount);
        Assert.Equal(topology.ShardCount, plan.WorkItems.Count);

        for (var shardId = 0; shardId < topology.ShardCount; shardId++)
        {
            var workItem = plan.WorkItems[shardId];
            Assert.Equal(shardId, workItem.WorkItemId);
            Assert.Equal(shardId, workItem.ShardId);
            Assert.Equal(shardId % group.Options.WorkerCount, workItem.WorkerId.Value);
            Assert.Equal(topology.Version, workItem.TopologyVersion);
            Assert.Equal(
                topology.Partitions
                    .Where(partition => partition.ShardId == shardId)
                    .Select(partition => partition.PartitionId)
                    .ToArray(),
                workItem.PartitionIds);
        }
    }

    [Fact]
    public async Task DispatchUsesOneCapturedTopologyVersionEvenIfTopologyMovesDuringExecution()
    {
        var manager = CreateTopologyManager(sourceCount: 6, partitionCount: 6, shardCount: 3);
        await using var group = CreateStartedGroup(workerCount: 3, queueCapacity: 1);
        var providerCallCount = 0;
        var dispatcher = new RadarProcessingAsyncBatchDispatcher(
            group,
            () =>
            {
                providerCallCount++;
                return manager.Current;
        });
        var batch = CreateEightBitBatch(manager.Current.SourceUniverseVersion, sourceIds: [1]);
        var capturedVersion = manager.Current.Version;
        var moved = 0;

        var result = await dispatcher.DispatchAsync(
            batchSequence: 1,
            batch,
            (borrowedBatch, route, workItem, _) =>
            {
                if (Interlocked.CompareExchange(ref moved, 1, 0) == 0)
                {
                    var move = manager.MovePartition(
                        new RadarProcessingTopologyMoveRequest(
                            capturedVersion,
                            partitionId: 1,
                            sourceShardId: 0,
                            targetShardId: 2));
                    Assert.True(move.Succeeded);
                }

                Assert.Equal(capturedVersion, route.TopologyVersion);
                Assert.Equal(capturedVersion, workItem.TopologyVersion);
                Assert.Equal(0, route.GetRoutedEvent(0).ShardId);
                return Succeed(workItem, CancellationToken.None);
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, Volatile.Read(ref moved));
        Assert.Equal(1, providerCallCount);
        Assert.Equal(capturedVersion, result.TopologyVersion);
        Assert.Equal(capturedVersion.Next(), manager.Current.Version);
    }

    [Fact]
    public async Task DispatchPassesBorrowedBatchAndRouteToExecutorWithoutPayloadCopy()
    {
        await using var group = CreateStartedGroup(workerCount: 2, queueCapacity: 1);
        var topology = CreateTopology(sourceCount: 4, partitionCount: 4, shardCount: 2);
        var dispatcher = new RadarProcessingAsyncBatchDispatcher(group, () => topology);
        var batch = CreateEightBitBatch(topology.SourceUniverseVersion, sourceIds: [0, 3]);
        var observedExecutorCalls = 0;
        RadarProcessingBatchRoute? observedRoute = null;

        var result = await dispatcher.DispatchAsync(
            batchSequence: 2,
            batch,
            (borrowedBatch, route, workItem, cancellationToken) =>
            {
                Assert.Same(batch, borrowedBatch);
                observedRoute ??= route;
                Assert.Same(observedRoute, route);
                Assert.Equal(2, borrowedBatch.Payload.Length);
                Assert.Equal(topology.Version, route.TopologyVersion);
                Interlocked.Increment(ref observedExecutorCalls);
                return Succeed(workItem, cancellationToken);
            });

        Assert.True(result.IsSuccess);
        Assert.Same(observedRoute, result.Route);
        Assert.Equal(topology.ShardCount, observedExecutorCalls);
        Assert.True(result.DrainResult.IsDrained);
    }

    [Fact]
    public void DispatcherRejectsRouteTopologyMismatch()
    {
        using var group = CreateStartedGroup(workerCount: 3, queueCapacity: 1);
        var manager = CreateTopologyManager(sourceCount: 6, partitionCount: 6, shardCount: 3);
        var dispatcher = new RadarProcessingAsyncBatchDispatcher(group, () => manager.Current);
        var batch = CreateEightBitBatch(manager.Current.SourceUniverseVersion, sourceIds: [1]);
        var firstTopology = manager.Current;
        var firstRoute = new RadarProcessingBatchRouter(firstTopology).Route(batch);

        var move = manager.MovePartition(
            new RadarProcessingTopologyMoveRequest(
                firstTopology.Version,
                partitionId: 1,
                sourceShardId: 0,
                targetShardId: 2));

        Assert.True(move.Succeeded);
        Assert.Throws<ArgumentException>(() =>
            dispatcher.CreatePlan(batchSequence: 1, manager.Current, firstRoute));
    }

    [Fact]
    public async Task DispatchCompletesOnlyAfterWorkerGroupCompletionBarrier()
    {
        await using var group = CreateStartedGroup(workerCount: 1, queueCapacity: 1);
        var topology = CreateTopology(sourceCount: 1, partitionCount: 1, shardCount: 1);
        var dispatcher = new RadarProcessingAsyncBatchDispatcher(group, () => topology);
        var batch = CreateEightBitBatch(topology.SourceUniverseVersion, sourceIds: [0]);
        var startedExecution = CreateSignal();
        var releaseExecution = CreateSignal();

        var dispatch = dispatcher.DispatchAsync(
            batchSequence: 3,
            batch,
            async (borrowedBatch, route, workItem, _) =>
            {
                Assert.Same(batch, borrowedBatch);
                Assert.Equal(topology.Version, route.TopologyVersion);
                startedExecution.SetResult();
                await releaseExecution.Task.ConfigureAwait(false);
                return RadarProcessingAsyncWorkCompletion.Succeeded(workItem);
            }).AsTask();

        await startedExecution.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(dispatch.IsCompleted);

        releaseExecution.SetResult();
        var result = await dispatch.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(result.IsSuccess);
        Assert.True(result.DrainResult.IsDrained);
        Assert.Equal(1, result.BatchResult?.Completion.SucceededWorkItemCount);
    }

    [Fact]
    public async Task DispatchReportsWorkerTimingAndCompletionStatus()
    {
        await using var group = CreateStartedGroup(workerCount: 2, queueCapacity: 1);
        var topology = CreateTopology(sourceCount: 4, partitionCount: 4, shardCount: 2);
        var dispatcher = new RadarProcessingAsyncBatchDispatcher(group, () => topology);
        var batch = CreateEightBitBatch(topology.SourceUniverseVersion, sourceIds: [0, 1, 2, 3]);

        var result = await dispatcher.DispatchAsync(
            batchSequence: 4,
            batch,
            (borrowedBatch, route, workItem, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var shard = route.GetShard(workItem.ShardId);
                return ValueTask.FromResult(
                    RadarProcessingAsyncWorkCompletion.Succeeded(
                        workItem,
                        processedStreamEventCount: shard.EventIndexes.Length,
                        processedPayloadValueCount: shard.Metrics.PayloadValueCount));
            });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.BatchResult);
        Assert.Equal(topology.ShardCount, result.BatchResult.Completion.SucceededWorkItemCount);
        Assert.Equal(batch.EventCount, result.BatchResult.Completion.ProcessedStreamEventCount);
        Assert.Equal(batch.EventCount, result.BatchResult.Completion.ProcessedPayloadValueCount);
        Assert.True(result.DrainResult.IsDrained);
        Assert.Equal(topology.ShardCount, result.DrainResult.AcceptedWorkItemCount);
        Assert.Equal(topology.ShardCount, result.DrainResult.CompletedWorkItemCount);
        Assert.True(result.DrainResult.BarrierWaitTime >= TimeSpan.Zero);
    }

    [Fact]
    public void DispatchContractsRejectInvalidShapes()
    {
        using var group = CreateStartedGroup(workerCount: 1, queueCapacity: 1);
        var topology = CreateTopology(sourceCount: 1, partitionCount: 1, shardCount: 1);
        var dispatcher = new RadarProcessingAsyncBatchDispatcher(group, () => topology);
        var batch = CreateEightBitBatch(topology.SourceUniverseVersion, sourceIds: [0]);
        var plan = dispatcher.CreatePlan(batchSequence: 1, batch);
        var workerResult = RadarProcessingAsyncWorkerGroupResult.Completed(
            new RadarProcessingWorkerGroupStatus(
                RadarProcessingWorkerGroupState.Running,
                RadarProcessingWorkerHealth.Healthy,
                workerCount: 1,
                queueCapacity: 1),
            plan.Scope.RecordCompletion(RadarProcessingAsyncWorkCompletion.Succeeded(plan.WorkItems[0])),
            new RadarProcessingAsyncWorkerGroupDrainResult());

        Assert.Throws<ArgumentNullException>(() => new RadarProcessingAsyncBatchDispatcher(null!, () => topology));
        Assert.Throws<ArgumentNullException>(() => new RadarProcessingAsyncBatchDispatcher(group, null!));
        Assert.Throws<ArgumentNullException>(() => dispatcher.CreatePlan(1, null!));
        Assert.Throws<ArgumentNullException>(() => dispatcher.CreatePlan(1, null!, plan.Route));
        Assert.Throws<ArgumentNullException>(() => dispatcher.CreatePlan(1, topology, null!));
        Assert.Throws<ArgumentNullException>(() => new RadarProcessingAsyncDispatchPlan(null!, plan.Route, plan.WorkItems));
        Assert.Throws<ArgumentNullException>(() => new RadarProcessingAsyncDispatchPlan(plan.Scope, null!, plan.WorkItems));
        Assert.Throws<ArgumentNullException>(() => new RadarProcessingAsyncDispatchPlan(plan.Scope, plan.Route, null!));
        Assert.Throws<ArgumentNullException>(() => new RadarProcessingAsyncDispatchResult(null!, workerResult));
        Assert.Throws<ArgumentNullException>(() => new RadarProcessingAsyncDispatchResult(plan, null!));
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

    private static ValueTask<RadarProcessingAsyncWorkCompletion> Succeed(
        RadarProcessingAsyncWorkItem workItem,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(
            RadarProcessingAsyncWorkCompletion.Succeeded(
                workItem,
                processedStreamEventCount: 1,
                processedPayloadValueCount: 1));
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

    private static RadarProcessingTopologyManager CreateTopologyManager(
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

    private static TaskCompletionSource CreateSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
