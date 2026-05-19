using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingAsyncValidatorTests
{
    [Fact]
    public void AsyncValidationErrorEnumValuesAreStable()
    {
        Assert.Equal(0, (int)RadarProcessingAsyncValidationError.None);
        Assert.Equal(1, (int)RadarProcessingAsyncValidationError.NonAsyncExecutionMode);
        Assert.Equal(2, (int)RadarProcessingAsyncValidationError.MissingWorkerTelemetry);
        Assert.Equal(3, (int)RadarProcessingAsyncValidationError.MissingProcessingTelemetry);
        Assert.Equal(4, (int)RadarProcessingAsyncValidationError.FailedBatchCompletion);
        Assert.Equal(5, (int)RadarProcessingAsyncValidationError.IncompleteBatchCompletion);
        Assert.Equal(6, (int)RadarProcessingAsyncValidationError.WorkerFailureNotPropagated);
        Assert.Equal(7, (int)RadarProcessingAsyncValidationError.TopologyVersionMismatch);
        Assert.Equal(8, (int)RadarProcessingAsyncValidationError.UnexpectedMigrationAfterFailedProcessing);
        Assert.Equal(9, (int)RadarProcessingAsyncValidationError.MissingWorkItem);
        Assert.Equal(10, (int)RadarProcessingAsyncValidationError.DuplicateWorkAssignment);
        Assert.Equal(11, (int)RadarProcessingAsyncValidationError.WorkItemScopeMismatch);
        Assert.Equal(12, (int)RadarProcessingAsyncValidationError.WorkItemShardOwnershipMismatch);
        Assert.Equal(13, (int)RadarProcessingAsyncValidationError.WorkItemWorkerAssignmentMismatch);
        Assert.Equal(14, (int)RadarProcessingAsyncValidationError.CompletionScopeMismatch);
        Assert.Equal(15, (int)RadarProcessingAsyncValidationError.CompletionStatusMismatch);
        Assert.Equal(16, (int)RadarProcessingAsyncValidationError.AggregationMetricMismatch);
        Assert.Equal(17, (int)RadarProcessingAsyncValidationError.TelemetryMetricMismatch);
        Assert.Equal(18, (int)RadarProcessingAsyncValidationError.DeterministicChecksumMismatch);
        Assert.Equal(19, (int)RadarProcessingAsyncValidationError.RetentionLimitExceeded);
    }

    [Fact]
    public void EssentialProfileCatchesFailedCompletionAndTopologyMismatch()
    {
        var route = CreateRoute();
        var workItems = CreateCanonicalWorkItems(route);
        var failedCompletion = CreateBatchResult(
            route,
            workItems,
            workItems[0].WorkItemId,
            RadarProcessingAsyncBatchCompletionError.WorkFailed);

        var failedResult = RadarProcessingAsyncValidator.ValidateTransport(
            route,
            workItems,
            failedCompletion,
            RadarProcessingValidationProfile.Essential);

        AssertInvalid(failedResult, RadarProcessingAsyncValidationError.FailedBatchCompletion);

        var mismatchedProcessingResult = CreateAsyncProcessingResult(
            route,
            workerTelemetryTopologyVersion: route.TopologyVersion.Next());

        var topologyResult = RadarProcessingAsyncValidator.ValidateProcessingResult(
            mismatchedProcessingResult,
            RadarProcessingValidationProfile.Essential);

        AssertInvalid(topologyResult, RadarProcessingAsyncValidationError.TopologyVersionMismatch);
    }

    [Fact]
    public void DiagnosticProfileCatchesMissingAndDuplicateWork()
    {
        var route = CreateRoute();
        var missingWorkItems = CreateWorkItems(
            route,
            new[]
            {
                new[] { 0 },
                new[] { 2, 3 }
            });
        var missingResult = RadarProcessingAsyncValidator.ValidateTransport(
            route,
            missingWorkItems,
            CreateBatchResult(route, missingWorkItems),
            RadarProcessingValidationProfile.Diagnostic,
            workerCount: 2);

        AssertInvalid(missingResult, RadarProcessingAsyncValidationError.MissingWorkItem);

        var duplicateWorkItems = CreateWorkItems(
            route,
            new[]
            {
                new[] { 0, 1 },
                new[] { 1, 2, 3 }
            });
        var duplicateResult = RadarProcessingAsyncValidator.ValidateTransport(
            route,
            duplicateWorkItems,
            CreateBatchResult(route, duplicateWorkItems),
            RadarProcessingValidationProfile.Diagnostic,
            workerCount: 2);

        AssertInvalid(duplicateResult, RadarProcessingAsyncValidationError.DuplicateWorkAssignment);
    }

    [Fact]
    public void DiagnosticProfileCatchesAssignmentOutsideShardOwnership()
    {
        var route = CreateRoute();
        var workItems = CreateWorkItems(
            route,
            new[]
            {
                new[] { 0, 2 },
                new[] { 1, 3 }
            });

        var result = RadarProcessingAsyncValidator.ValidateTransport(
            route,
            workItems,
            CreateBatchResult(route, workItems),
            RadarProcessingValidationProfile.Diagnostic,
            workerCount: 2);

        AssertInvalid(result, RadarProcessingAsyncValidationError.WorkItemShardOwnershipMismatch);
    }

    [Fact]
    public async Task BenchmarkProfileComparesSyncAndAsyncChecksums()
    {
        var universe = CreateUniverse(sourceCount: 6);
        var syncCore = CreateCore(
            universe,
            RadarProcessingExecutionMode.PartitionedBarrier,
            partitionCount: 6,
            shardCount: 3);
        var asyncCore = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 6,
            shardCount: 3,
            asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 3, queueCapacity: 1));
        var batch = CreateMixedBatch(universe.Version);

        var syncResult = syncCore.Process(batch);
        await using var asyncSession = new RadarProcessingAsyncCoreSession(asyncCore);
        var asyncResult = await asyncSession.ProcessAsync(batch);

        var result = RadarProcessingAsyncValidator.ValidateBenchmarkComparison(
            syncResult,
            asyncResult,
            syncCore.CreateSourceSnapshots(),
            asyncCore.CreateSourceSnapshots());

        Assert.True(result.IsValid);
        Assert.True(result.HasComparisonChecksums);
        Assert.Equal(syncResult.Metrics.ProcessingChecksum, result.SynchronousChecksum);
        Assert.Equal(asyncResult.Metrics.ProcessingChecksum, result.AsyncChecksum);
    }

    [Fact]
    public void WorkerTelemetryRetentionValidationObeysBounds()
    {
        var options = new RadarProcessingTelemetryRetentionOptions(
            RadarProcessingDiagnosticRetentionMode.Recent,
            maxRetainedWorkerBatches: 1,
            maxRetainedWorkerFailures: 1);
        var valid = new RadarProcessingWorkerTelemetrySummary(
            new RadarProcessingWorkerTelemetryCounters(
                dispatchedBatchCount: 2,
                failedBatchCount: 2,
                rejectedDispatchCount: 2),
            workerCount: 1,
            queueCapacity: 1,
            new[]
            {
                new RadarProcessingRecentWorkerBatch(
                    batchSequence: 2,
                    topologyVersion: RadarProcessingTopologyVersion.Initial,
                    workerCount: 1,
                    queueCapacity: 1,
                    submittedWorkItemCount: 1,
                    acceptedWorkItemCount: 0,
                    completedWorkItemCount: 0,
                    succeededWorkItemCount: 0,
                    failedWorkItemCount: 0,
                    canceledWorkItemCount: 0,
                    isSuccessful: false,
                    isRejected: true,
                    timedOut: false,
                    failureKind: RadarProcessingAsyncFailureKind.EnqueueRejected)
            },
            new[]
            {
                new RadarProcessingRecentWorkerFailure(
                    batchSequence: 2,
                    topologyVersion: RadarProcessingTopologyVersion.Initial,
                    failureKind: RadarProcessingAsyncFailureKind.EnqueueRejected)
            },
            new RadarProcessingWorkerRetentionStats(
                retainedBatchCount: 1,
                droppedBatchCount: 1,
                retainedFailureCount: 1,
                droppedFailureCount: 1));

        var validResult = RadarProcessingAsyncValidator.ValidateWorkerTelemetryRetention(
            valid,
            options);

        Assert.True(validResult.IsValid);

        var invalid = new RadarProcessingWorkerTelemetrySummary(
            valid.Counters,
            workerCount: 1,
            queueCapacity: 1,
            new[]
            {
                valid.RecentBatches[0],
                new RadarProcessingRecentWorkerBatch(
                    batchSequence: 3,
                    topologyVersion: RadarProcessingTopologyVersion.Initial,
                    workerCount: 1,
                    queueCapacity: 1,
                    submittedWorkItemCount: 1,
                    acceptedWorkItemCount: 0,
                    completedWorkItemCount: 0,
                    succeededWorkItemCount: 0,
                    failedWorkItemCount: 0,
                    canceledWorkItemCount: 0,
                    isSuccessful: false,
                    isRejected: true,
                    timedOut: false,
                    failureKind: RadarProcessingAsyncFailureKind.EnqueueRejected)
            },
            valid.RecentFailures,
            new RadarProcessingWorkerRetentionStats(
                retainedBatchCount: 2,
                retainedFailureCount: 1));

        var invalidResult = RadarProcessingAsyncValidator.ValidateWorkerTelemetryRetention(
            invalid,
            options);

        AssertInvalid(invalidResult, RadarProcessingAsyncValidationError.RetentionLimitExceeded);
    }

    private static void AssertInvalid(
        RadarProcessingAsyncValidationResult result,
        RadarProcessingAsyncValidationError expectedError)
    {
        Assert.False(result.IsValid);
        Assert.Equal(expectedError, result.Error);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    private static RadarProcessingBatchRoute CreateRoute()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = CreateCore(
            universe,
            RadarProcessingExecutionMode.PartitionedBarrier,
            partitionCount: 4,
            shardCount: 2);

        return new RadarProcessingBatchRouter(core.Topology).Route(CreateEmptyBatch(universe.Version));
    }

    private static IReadOnlyList<RadarProcessingAsyncWorkItem> CreateCanonicalWorkItems(
        RadarProcessingBatchRoute route) =>
        CreateWorkItems(
            route,
            new[]
            {
                new[] { 0, 1 },
                new[] { 2, 3 }
            });

    private static IReadOnlyList<RadarProcessingAsyncWorkItem> CreateWorkItems(
        RadarProcessingBatchRoute route,
        IReadOnlyList<int[]> partitionIds)
    {
        var scope = new RadarProcessingAsyncBatchScope(
            batchSequence: 1,
            route.TopologyVersion,
            expectedWorkItemCount: partitionIds.Count);
        var workItems = new RadarProcessingAsyncWorkItem[partitionIds.Count];
        for (var workItemId = 0; workItemId < workItems.Length; workItemId++)
        {
            workItems[workItemId] = scope.CreateWorkItem(
                workItemId,
                new RadarProcessingWorkerId(workItemId),
                shardId: workItemId,
                partitionIds[workItemId]);
        }

        return Array.AsReadOnly(workItems);
    }

    private static RadarProcessingAsyncBatchScopeResult CreateBatchResult(
        RadarProcessingBatchRoute route,
        IReadOnlyList<RadarProcessingAsyncWorkItem> workItems,
        int failedWorkItemId = -1,
        RadarProcessingAsyncBatchCompletionError error = RadarProcessingAsyncBatchCompletionError.None)
    {
        var completions = new RadarProcessingAsyncWorkCompletion[workItems.Count];
        for (var i = 0; i < completions.Length; i++)
        {
            var workItem = workItems[i];
            var shard = route.GetShard(workItem.ShardId);
            completions[i] = i == failedWorkItemId
                ? RadarProcessingAsyncWorkCompletion.Failed(workItem)
                : RadarProcessingAsyncWorkCompletion.Succeeded(
                    workItem,
                    processedStreamEventCount: shard.Metrics.EventCount,
                    processedPayloadValueCount: shard.Metrics.PayloadValueCount);
        }

        return new RadarProcessingAsyncBatchScopeResult(
            new RadarProcessingAsyncBatchCompletion(
                batchSequence: 1,
                route.TopologyVersion,
                workItems.Count,
                completions,
                isClosed: true),
            error);
    }

    private static RadarProcessingResult CreateAsyncProcessingResult(
        RadarProcessingBatchRoute route,
        RadarProcessingTopologyVersion? workerTelemetryTopologyVersion = null)
    {
        var metrics = new RadarProcessingMetrics(
            ProcessedBatchCount: 1,
            ProcessedStreamEventCount: 0,
            ProcessedPayloadValueCount: 0,
            ActiveSourceCount: 0,
            RawValueChecksum: 0,
            ProcessingChecksum: 0);
        var workerTelemetry = new RadarProcessingWorkerTelemetrySummary(
            new RadarProcessingWorkerTelemetryCounters(
                dispatchedBatchCount: 1,
                completedBatchCount: 1,
                submittedWorkItemCount: route.ShardCount,
                acceptedWorkItemCount: route.ShardCount,
                completedWorkItemCount: route.ShardCount,
                succeededWorkItemCount: route.ShardCount),
            workerCount: route.ShardCount,
            queueCapacity: 1,
            new[]
            {
                new RadarProcessingRecentWorkerBatch(
                    batchSequence: 1,
                    topologyVersion: workerTelemetryTopologyVersion ?? route.TopologyVersion,
                    workerCount: route.ShardCount,
                    queueCapacity: 1,
                    submittedWorkItemCount: route.ShardCount,
                    acceptedWorkItemCount: route.ShardCount,
                    completedWorkItemCount: route.ShardCount,
                    succeededWorkItemCount: route.ShardCount,
                    failedWorkItemCount: 0,
                    canceledWorkItemCount: 0,
                    isSuccessful: true,
                    isRejected: false,
                    timedOut: false)
            },
            Array.Empty<RadarProcessingRecentWorkerFailure>(),
            new RadarProcessingWorkerRetentionStats(retainedBatchCount: 1));

        return new RadarProcessingResult(
            RadarProcessingExecutionMode.AsyncShardTransport,
            route.PartitionCount,
            route.ShardCount,
            metrics,
            RadarProcessingValidationResult.Valid(metrics),
            RadarProcessingTelemetry.FromRoute(RadarProcessingExecutionMode.AsyncShardTransport, route),
            route.TopologyVersion,
            workerTelemetry);
    }

    private static RadarProcessingCore CreateCore(
        RadarSourceUniverse universe,
        RadarProcessingExecutionMode mode,
        int partitionCount,
        int shardCount,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null) =>
        new(
            universe,
            new RadarProcessingCoreOptions(
                mode,
                partitionCount,
                shardCount,
                asyncExecution: asyncExecution));

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarEventBatch CreateEmptyBatch(SourceUniverseVersion sourceUniverseVersion) =>
        new(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            Array.Empty<RadarStreamEvent>(),
            Array.Empty<byte>());

    private static RadarEventBatch CreateMixedBatch(SourceUniverseVersion sourceUniverseVersion) =>
        new(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            new[]
            {
                CreateEvent(sourceId: 0, messageTimestampUtcTicks: 100, payloadOffset: 0, gateCount: 4),
                CreateEvent(sourceId: 3, messageTimestampUtcTicks: 101, payloadOffset: 4, gateCount: 2, wordSize: RadarStreamWordSize.SixteenBit),
                CreateEvent(sourceId: 1, messageTimestampUtcTicks: 102, payloadOffset: 8, gateCount: 4),
                CreateEvent(sourceId: 5, messageTimestampUtcTicks: 103, payloadOffset: 12, gateCount: 2, wordSize: RadarStreamWordSize.SixteenBit),
                CreateEvent(sourceId: 3, messageTimestampUtcTicks: 104, payloadOffset: 16, gateCount: 4)
            },
            new byte[]
            {
                1, 2, 3, 4,
                0, 5, 1, 0,
                5, 6, 7, 8,
                2, 0, 0, 1,
                9, 10, 11, 12
            });

    private static RadarStreamEvent CreateEvent(
        int sourceId,
        long messageTimestampUtcTicks,
        int payloadOffset,
        ushort gateCount = 1,
        RadarStreamWordSize wordSize = RadarStreamWordSize.EightBit)
    {
        var payloadLength = checked(gateCount * (wordSize == RadarStreamWordSize.EightBit ? 1 : 2));
        return new RadarStreamEvent(
            sourceId,
            radarOrdinal: 0,
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks,
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
