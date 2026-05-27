using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingWorkerTelemetryRecorderTests
{
    [Fact]
    public async Task RecorderAggregatesSuccessfulDispatchCountersAndTiming()
    {
        await using var group = CreateStartedGroup(workerCount: 2, queueCapacity: 1);
        var topology = CreateTopology(sourceCount: 4, partitionCount: 4, shardCount: 2);
        var dispatcher = new RadarProcessingAsyncBatchDispatcher(group, () => topology);
        var batch = CreateEightBitBatch(topology.SourceUniverseVersion, sourceIds: [0, 1, 2, 3]);
        var dispatch = await dispatcher.DispatchAsync(
            batchSequence: 7,
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
        var recorder = new RadarProcessingWorkerTelemetryRecorder();

        recorder.RecordDispatch(
            dispatch,
            dispatchTime: TimeSpan.FromMilliseconds(9),
            aggregationTime: TimeSpan.FromMilliseconds(3));
        var summary = recorder.CreateSummary();

        Assert.Equal(2, summary.WorkerCount);
        Assert.Equal(1, summary.QueueCapacity);
        Assert.Equal(1, summary.Counters.DispatchedBatchCount);
        Assert.Equal(1, summary.Counters.CompletedBatchCount);
        Assert.Equal(0, summary.Counters.FailedBatchCount);
        Assert.Equal(0, summary.Counters.CanceledBatchCount);
        Assert.Equal(2, summary.Counters.SubmittedWorkItemCount);
        Assert.Equal(2, summary.Counters.AcceptedWorkItemCount);
        Assert.Equal(2, summary.Counters.CompletedWorkItemCount);
        Assert.Equal(2, summary.Counters.SucceededWorkItemCount);
        Assert.Equal(TimeSpan.FromMilliseconds(9), summary.Counters.TotalDispatchTime);
        Assert.Equal(TimeSpan.FromMilliseconds(3), summary.Counters.TotalAggregationTime);
        Assert.Equal(dispatch.BatchResult!.Completion.TotalQueueWaitTime, summary.Counters.TotalQueueWaitTime);
        Assert.Equal(dispatch.BatchResult.Completion.TotalExecutionTime, summary.Counters.TotalExecutionTime);
        Assert.Equal(dispatch.DrainResult.BarrierWaitTime, summary.Counters.TotalBarrierWaitTime);
        Assert.Single(summary.RecentBatches);
        Assert.Empty(summary.RecentFailures);
        Assert.Equal(7, summary.RecentBatches[0].BatchSequence);
        Assert.True(summary.RecentBatches[0].IsSuccessful);
    }

    [Fact]
    public void RecorderRetainsBoundedRecentBatchesAndFailures()
    {
        var recorder = new RadarProcessingWorkerTelemetryRecorder(
            new RadarProcessingTelemetryRetentionOptions(
                RadarProcessingDiagnosticRetentionMode.Recent,
                maxRetainedWorkerBatches: 2,
                maxRetainedWorkerFailures: 1));

        recorder.RecordDispatch(CreateDispatch(CreateSucceededResult(CreatePlan(batchSequence: 1))));
        recorder.RecordDispatch(CreateDispatch(CreateFailedResult(CreatePlan(batchSequence: 2))));
        recorder.RecordDispatch(CreateDispatch(CreateTimedOutResult(CreatePlan(batchSequence: 3))));
        var summary = recorder.CreateSummary();

        Assert.Equal(3, summary.Counters.DispatchedBatchCount);
        Assert.Equal(1, summary.Counters.CompletedBatchCount);
        Assert.Equal(2, summary.Counters.FailedBatchCount);
        Assert.Equal(1, summary.Counters.TimedOutBatchCount);
        Assert.Equal(1, summary.Counters.RejectedDispatchCount);
        Assert.Equal(new long[] { 2, 3 }, summary.RecentBatches.Select(static batch => batch.BatchSequence));
        Assert.Single(summary.RecentFailures);
        Assert.Equal(3, summary.RecentFailures[0].BatchSequence);
        Assert.Equal(RadarProcessingAsyncFailureKind.TimedOut, summary.RecentFailures[0].FailureKind);
        Assert.True(summary.RecentFailures[0].TimedOut);
        Assert.Equal(1, summary.RetentionStats.DroppedBatchCount);
        Assert.Equal(1, summary.RetentionStats.RetainedFailureCount);
        Assert.True(summary.RetentionStats.DroppedFailureCount > 0);
        Assert.True(summary.RetentionStats.HasDroppedDetail);
    }

    [Fact]
    public void RecorderCountersOnlyRetentionDropsAllRecentDetail()
    {
        var recorder = new RadarProcessingWorkerTelemetryRecorder(
            new RadarProcessingTelemetryRetentionOptions(
                RadarProcessingDiagnosticRetentionMode.Counters,
                maxRetainedWorkerBatches: 10,
                maxRetainedWorkerFailures: 10));

        recorder.RecordDispatch(CreateDispatch(CreateFailedResult(CreatePlan(batchSequence: 1))));
        var summary = recorder.CreateSummary();

        Assert.Equal(1, summary.Counters.DispatchedBatchCount);
        Assert.Equal(1, summary.Counters.FailedBatchCount);
        Assert.Equal(1, summary.Counters.FailedWorkItemCount);
        Assert.Empty(summary.RecentBatches);
        Assert.Empty(summary.RecentFailures);
        Assert.Equal(1, summary.RetentionStats.DroppedBatchCount);
        Assert.Equal(1, summary.RetentionStats.DroppedFailureCount);
    }

    [Fact]
    public void RecorderRecordsCancellationCodesWithoutFailureText()
    {
        var recorder = new RadarProcessingWorkerTelemetryRecorder();
        var plan = CreatePlan(batchSequence: 5);

        recorder.RecordDispatch(CreateDispatch(CreateCanceledResult(plan)));
        var summary = recorder.CreateSummary();

        Assert.Equal(1, summary.Counters.DispatchedBatchCount);
        Assert.Equal(0, summary.Counters.FailedBatchCount);
        Assert.Equal(1, summary.Counters.CanceledBatchCount);
        Assert.Equal(2, summary.Counters.CanceledWorkItemCount);
        Assert.Equal(2, summary.RecentFailures.Count);
        Assert.All(
            summary.RecentFailures,
            static failure =>
            {
                Assert.Equal(RadarProcessingAsyncFailureKind.None, failure.FailureKind);
                Assert.Equal(RadarProcessingAsyncCancellationKind.BeforeDispatch, failure.CancellationKind);
                Assert.NotNull(failure.WorkItemId);
            });
    }

    [Fact]
    public void RecorderSummarySnapshotIsStableAfterLaterMutationsAndCanReset()
    {
        var recorder = new RadarProcessingWorkerTelemetryRecorder();

        recorder.RecordDispatch(CreateDispatch(CreateSucceededResult(CreatePlan(batchSequence: 1))));
        var first = recorder.CreateSummary();
        recorder.RecordDispatch(CreateDispatch(CreateSucceededResult(CreatePlan(batchSequence: 2))));
        var second = recorder.CreateSummary();
        recorder.Reset();
        var reset = recorder.CreateSummary();

        Assert.Equal(1, first.Counters.DispatchedBatchCount);
        Assert.Single(first.RecentBatches);
        Assert.Equal(1, first.RecentBatches[0].BatchSequence);
        Assert.Equal(2, second.Counters.DispatchedBatchCount);
        Assert.Equal(2, second.RecentBatches.Count);
        Assert.Equal(0, reset.Counters.DispatchedBatchCount);
        Assert.Empty(reset.RecentBatches);
        Assert.Equal(0, reset.WorkerCount);
        Assert.Throws<ArgumentNullException>(() => recorder.RecordDispatch(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            recorder.RecordDispatch(CreateDispatch(CreateSucceededResult(CreatePlan(batchSequence: 3))), dispatchTime: TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            recorder.RecordDispatch(CreateDispatch(CreateSucceededResult(CreatePlan(batchSequence: 4))), aggregationTime: TimeSpan.FromTicks(-1)));
    }

    private static RadarProcessingAsyncDispatchResult CreateDispatch(
        RadarProcessingAsyncDispatchPlan plan,
        RadarProcessingAsyncWorkerGroupResult workerResult) =>
        new(plan, workerResult);

    private static RadarProcessingAsyncDispatchResult CreateDispatch(
        (RadarProcessingAsyncDispatchPlan Plan, RadarProcessingAsyncWorkerGroupResult WorkerResult) pair) =>
        new(pair.Plan, pair.WorkerResult);

    private static (RadarProcessingAsyncDispatchPlan Plan, RadarProcessingAsyncWorkerGroupResult WorkerResult)
        CreateSucceededResult(RadarProcessingAsyncDispatchPlan plan)
    {
        var completions = CreateSucceededCompletions(plan);
        var batchResult = CreateBatchResult(plan, completions);
        return (
            plan,
            RadarProcessingAsyncWorkerGroupResult.Completed(
                CreateHealthyStatus(plan),
                batchResult,
                new RadarProcessingAsyncWorkerGroupDrainResult(
                    acceptedWorkItemCount: completions.Length,
                    completedWorkItemCount: completions.Length,
                    barrierWaitTime: TimeSpan.FromMilliseconds(4))));
    }

    private static (RadarProcessingAsyncDispatchPlan Plan, RadarProcessingAsyncWorkerGroupResult WorkerResult)
        CreateFailedResult(RadarProcessingAsyncDispatchPlan plan)
    {
        var completions = new[]
        {
            RadarProcessingAsyncWorkCompletion.Failed(
                plan.WorkItems[0],
                failureKind: RadarProcessingAsyncFailureKind.WorkerException),
            CreateSucceededCompletion(plan, workItemId: 1)
        };
        var batchResult = CreateBatchResult(
            plan,
            completions,
            RadarProcessingAsyncBatchCompletionError.WorkFailed);
        return (
            plan,
            RadarProcessingAsyncWorkerGroupResult.Completed(
                CreateHealthyStatus(plan),
                batchResult,
                new RadarProcessingAsyncWorkerGroupDrainResult(
                    acceptedWorkItemCount: completions.Length,
                    completedWorkItemCount: completions.Length)));
    }

    private static (RadarProcessingAsyncDispatchPlan Plan, RadarProcessingAsyncWorkerGroupResult WorkerResult)
        CreateCanceledResult(RadarProcessingAsyncDispatchPlan plan)
    {
        var completions = plan.WorkItems
            .Select(static workItem =>
                RadarProcessingAsyncWorkCompletion.Canceled(
                    workItem,
                    cancellationKind: RadarProcessingAsyncCancellationKind.BeforeDispatch))
            .ToArray();
        var batchResult = CreateBatchResult(
            plan,
            completions,
            RadarProcessingAsyncBatchCompletionError.WorkCanceled);
        return (
            plan,
            RadarProcessingAsyncWorkerGroupResult.Completed(
                CreateHealthyStatus(plan),
                batchResult,
                new RadarProcessingAsyncWorkerGroupDrainResult(
                    completedWorkItemCount: completions.Length,
                    cancellationRequested: true),
                cancellationKind: RadarProcessingAsyncCancellationKind.BeforeDispatch));
    }

    private static (RadarProcessingAsyncDispatchPlan Plan, RadarProcessingAsyncWorkerGroupResult WorkerResult)
        CreateTimedOutResult(RadarProcessingAsyncDispatchPlan plan)
    {
        var completions = CreateSucceededCompletions(plan);
        var batchResult = CreateBatchResult(plan, completions);
        var previous = CreateHealthyStatus(plan);
        var current = new RadarProcessingWorkerGroupStatus(
            RadarProcessingWorkerGroupState.Faulted,
            RadarProcessingWorkerHealth.Faulted,
            plan.ShardCount,
            queueCapacity: 1,
            RadarProcessingWorkerLifecycleError.Faulted);
        return (
            plan,
            RadarProcessingAsyncWorkerGroupResult.Rejected(
                current,
                RadarProcessingAsyncWorkerGroupError.TimedOut,
                batchResult,
                new RadarProcessingAsyncWorkerGroupDrainResult(
                    acceptedWorkItemCount: completions.Length,
                    completedWorkItemCount: completions.Length,
                    barrierWaitTime: TimeSpan.FromMilliseconds(7),
                    timedOut: true),
                RadarProcessingAsyncFailureKind.TimedOut,
                timeoutResult: new RadarProcessingAsyncTimeoutResult(
                    timedOut: true,
                    timeout: TimeSpan.FromMilliseconds(5),
                    timeoutPolicy: RadarProcessingWorkerTimeoutPolicy.MarkUnhealthy),
                healthTransition: new RadarProcessingWorkerGroupHealthTransition(
                    previous,
                    current,
                    RadarProcessingAsyncFailureKind.TimedOut)));
    }

    private static RadarProcessingAsyncDispatchPlan CreatePlan(
        long batchSequence)
    {
        using var group = new RadarProcessingAsyncWorkerGroup(
            new RadarProcessingAsyncWorkerGroupOptions(
                new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 1)));
        var topology = CreateTopology(sourceCount: 4, partitionCount: 4, shardCount: 2);
        var batch = CreateEightBitBatch(topology.SourceUniverseVersion, sourceIds: [0, 1, 2, 3]);
        return new RadarProcessingAsyncBatchDispatcher(group, () => topology).CreatePlan(batchSequence, batch);
    }

    private static RadarProcessingWorkerGroupStatus CreateHealthyStatus(
        RadarProcessingAsyncDispatchPlan plan) =>
        new(
            RadarProcessingWorkerGroupState.Running,
            RadarProcessingWorkerHealth.Healthy,
            plan.ShardCount,
            queueCapacity: 1);

    private static RadarProcessingAsyncWorkCompletion[] CreateSucceededCompletions(
        RadarProcessingAsyncDispatchPlan plan) =>
        plan.WorkItems
            .Select(workItem => CreateSucceededCompletion(plan, workItem.WorkItemId))
            .ToArray();

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
