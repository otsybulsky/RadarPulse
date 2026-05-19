using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingQueuedRebalanceSessionTests
{
    [Fact]
    public async Task QueuedSyncRebalancePreservesAcceptedMoveCountsAgainstDirectReference()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var reference = CreateSession(universe);
        var queued = CreateSession(universe);
        var batches = new[]
        {
            CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1]),
            CreateEmptyBatch(universe.Version)
        };
        var referenceResults = batches.Select(batch => reference.Process(batch)).ToArray();
        using var session = new RadarProcessingQueuedRebalanceSession(
            queued,
            new RadarProcessingProviderQueueOptions(capacity: 2));

        foreach (var batch in batches)
        {
            var enqueue = await session.EnqueueAsync(batch);
            Assert.True(enqueue.IsAccepted);
        }

        session.CompleteAdding();
        var result = await session.DrainAsync();

        Assert.True(result.IsCompleted);
        Assert.Equal(reference.CurrentTopology.Version, queued.CurrentTopology.Version);
        Assert.Equal(reference.CurrentTopology.Version, result.FinalTopologyVersion);
        Assert.Equal([0L, 1L], result.ProcessingResults.Select(static processing => processing.Sequence.Value));
        Assert.All(result.ProcessingResults, static processing => Assert.True(processing.IsSuccessful));
        Assert.Equal(
            referenceResults.Count(static rebalance => rebalance.PublishedMigration),
            result.ProcessingResults.Count(static processing => processing.RebalanceResult?.PublishedMigration == true));
        Assert.Equal(
            referenceResults[^1].TelemetrySummary.Counters.AcceptedMoveCount,
            result.ProcessingResults[^1].RebalanceResult?.TelemetrySummary.Counters.AcceptedMoveCount);
        Assert.Equal(
            referenceResults[^1].ProcessingResult.TopologyVersion,
            result.ProcessingResults[^1].RebalanceResult?.ProcessingResult.TopologyVersion);
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), result.ProcessingResults[^1].TopologyVersion);
        Assert.Equal(2, result.Telemetry.CompletedBatchCount);
        Assert.Equal(2, result.Telemetry.DequeuedBatchCount);
    }

    [Fact]
    public async Task QueuedSyncRebalancePreservesNoActionCountsAgainstDirectReference()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var reference = CreateSession(universe);
        var queued = CreateSession(universe);
        var batches = new[]
        {
            CreateEmptyBatch(universe.Version),
            CreateEmptyBatch(universe.Version)
        };
        var referenceResults = batches.Select(batch => reference.Process(batch)).ToArray();
        using var session = new RadarProcessingQueuedRebalanceSession(
            queued,
            new RadarProcessingProviderQueueOptions(capacity: 2));

        foreach (var batch in batches)
        {
            var enqueue = await session.EnqueueAsync(batch);
            Assert.True(enqueue.IsAccepted);
        }

        session.CompleteAdding();
        var result = await session.DrainAsync();

        Assert.True(result.IsCompleted);
        Assert.Equal(reference.CurrentTopology.Version, result.FinalTopologyVersion);
        Assert.Equal(reference.CurrentTopology.Version, queued.CurrentTopology.Version);
        Assert.All(result.ProcessingResults, static processing => Assert.True(processing.IsSuccessful));
        Assert.Equal(
            referenceResults[^1].TelemetrySummary.Counters.NoActionDecisionCount,
            result.ProcessingResults[^1].RebalanceResult?.TelemetrySummary.Counters.NoActionDecisionCount);
        Assert.Equal(
            referenceResults[^1].TelemetrySummary.Counters.AcceptedMoveCount,
            result.ProcessingResults[^1].RebalanceResult?.TelemetrySummary.Counters.AcceptedMoveCount);
    }

    [Fact]
    public async Task QueuedAsyncRebalanceValidatesThroughAsyncWrapper()
    {
        var universe = CreateUniverse(sourceCount: 4);
        await using var session = new RadarProcessingQueuedRebalanceSession(
            CreateSession(
                universe,
                RadarProcessingExecutionMode.AsyncShardTransport,
                asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 1)),
            new RadarProcessingProviderQueueOptions(capacity: 1));

        var enqueue = await session.EnqueueAsync(
            CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1]));
        session.CompleteAdding();
        var result = await session.DrainAsync();

        Assert.True(enqueue.IsAccepted);
        Assert.True(result.IsCompleted);
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), result.FinalTopologyVersion);
        var processing = Assert.Single(result.ProcessingResults);
        Assert.True(processing.IsSuccessful);
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, processing.ProcessingResult?.ExecutionMode);
        Assert.NotNull(processing.RebalanceResult?.WorkerTelemetry);
        Assert.True(processing.RebalanceResult.Validation.IsValid);
        Assert.True(processing.RebalanceResult.PublishedMigration);
        Assert.Equal(1, processing.RebalanceResult.TelemetrySummary.Counters.AcceptedMoveCount);
        Assert.Equal(1, result.Telemetry.CompletedBatchCount);
    }

    [Fact]
    public async Task InvalidQueuedBatchFaultsRebalanceSessionSkipsAcceptedRemainderAndRejectsLaterEnqueue()
    {
        var universe = CreateUniverse(sourceCount: 1);
        using var session = new RadarProcessingQueuedRebalanceSession(
            CreateSession(universe, shardCount: 1),
            new RadarProcessingProviderQueueOptions(capacity: 2));

        var bad = await session.EnqueueAsync(CreateInvalidSourceBatch(universe.Version));
        var acceptedBeforeFault = await session.EnqueueAsync(
            CreateEightBitBatch(universe.Version, [0]));

        var result = await session.DrainAsync();
        var rejectedAfterFault = await session.EnqueueAsync(
            CreateEightBitBatch(universe.Version, [0]));

        Assert.True(bad.IsAccepted);
        Assert.True(acceptedBeforeFault.IsAccepted);
        Assert.True(result.IsFaulted);
        Assert.Equal(RadarProcessingQueuedSessionStatus.Faulted, result.Status);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, result.FinalTopologyVersion);
        Assert.Equal(2, result.ProcessingResults.Count);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.FailedValidation, result.ProcessingResults[0].Status);
        Assert.Equal(RadarProcessingValidationError.SourceIdOutsideUniverse, result.ProcessingResults[0].ProcessingResult?.Validation.Error);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault, result.ProcessingResults[1].Status);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Faulted, rejectedAfterFault.Status);
        Assert.Equal(1, result.Telemetry.FailedBatchCount);
        Assert.Equal(1, result.Telemetry.SkippedAfterFaultCount);
    }

    [Fact]
    public async Task DrainCancellationBeforeDequeueReturnsCanceledSessionResult()
    {
        var universe = CreateUniverse(sourceCount: 1);
        using var session = new RadarProcessingQueuedRebalanceSession(
            CreateSession(universe, shardCount: 1));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await session.DrainAsync(cancellation.Token);

        Assert.True(result.IsCanceled);
        Assert.Equal(RadarProcessingQueuedSessionStatus.Canceled, result.Status);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, result.FinalTopologyVersion);
        Assert.Empty(result.ProcessingResults);
        Assert.Equal(0, result.Telemetry.CompletedBatchCount);
        Assert.Equal(0, result.Telemetry.FailedBatchCount);
    }

    [Fact]
    public async Task QueuedRebalanceSessionRejectsInvalidAsyncComposition()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var syncRebalance = CreateSession(universe, shardCount: 1);
        var asyncRebalance = CreateSession(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            shardCount: 1);
        var otherAsyncRebalance = CreateSession(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            shardCount: 1);
        var asyncSession = new RadarProcessingAsyncRebalanceSession(asyncRebalance);
        var otherAsyncSession = new RadarProcessingAsyncRebalanceSession(otherAsyncRebalance);

        try
        {
            Assert.Throws<ArgumentException>(() =>
                new RadarProcessingQueuedRebalanceSession(
                    syncRebalance,
                    new RadarProcessingOwnedBatchQueue(),
                    asyncSession));
            Assert.Throws<ArgumentNullException>(() =>
                new RadarProcessingQueuedRebalanceSession(
                    asyncRebalance,
                    new RadarProcessingOwnedBatchQueue(),
                    asyncRebalanceSession: null));
            Assert.Throws<ArgumentException>(() =>
                new RadarProcessingQueuedRebalanceSession(
                    asyncRebalance,
                    new RadarProcessingOwnedBatchQueue(),
                    otherAsyncSession));
        }
        finally
        {
            await asyncSession.DisposeAsync();
            await otherAsyncSession.DisposeAsync();
        }
    }

    private static RadarProcessingRebalanceSession CreateSession(
        RadarSourceUniverse universe,
        RadarProcessingExecutionMode executionMode = RadarProcessingExecutionMode.PartitionedBarrier,
        int? shardCount = null,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null)
    {
        var effectiveShardCount = shardCount ?? Math.Min(2, universe.SourceCount);
        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                executionMode,
                partitionCount: universe.SourceCount,
                shardCount: effectiveShardCount,
                asyncExecution: asyncExecution));

        return new RadarProcessingRebalanceSession(
            core,
            CreatePressureOptions(),
            CreatePressureWindow(),
            CreatePolicyState(universe.SourceCount, effectiveShardCount),
            telemetryRecorder: CreateTelemetryRecorder());
    }

    private static RadarProcessingPressureOptions CreatePressureOptions() =>
        new(
            eventWeight: 1.0,
            payloadValueWeight: 0.0,
            rawValueChecksumWeight: 0.0);

    private static RadarProcessingPressureWindow CreatePressureWindow() =>
        new(
            new RadarProcessingPressureWindowOptions(
                sampleCapacity: 2,
                minimumSampleCount: 1,
                coldThreshold: 0.0,
                warmExitThreshold: 4.0,
                warmEnterThreshold: 4.5,
                hotExitThreshold: 4.75,
                hotEnterThreshold: 5.0,
                superHotExitThreshold: 9.0,
                superHotEnterThreshold: 10.0));

    private static RadarProcessingRebalancePolicyState CreatePolicyState(
        int partitionCount,
        int shardCount) =>
        new(
            partitionCount,
            shardCount,
            new RadarProcessingRebalanceOptions(
                budgetWindowEvaluationCount: 4,
                globalMoveBudgetPerWindow: 4,
                sourceShardMoveBudgetPerWindow: 4,
                targetShardReceiveBudgetPerWindow: 4,
                minimumPartitionResidencyEvaluations: 0,
                partitionMoveCooldownEvaluations: 0,
                sourceShardMoveCooldownEvaluations: 0,
                targetShardReceiveCooldownEvaluations: 0,
                minimumProjectedBenefit: 0.05));

    private static RadarProcessingRebalanceTelemetryRecorder CreateTelemetryRecorder() =>
        new(
            new RadarProcessingTelemetryRetentionOptions(
                RadarProcessingDiagnosticRetentionMode.Recent,
                maxRetainedDecisions: 8,
                maxRetainedLifecycleTransitions: 8,
                maxRetainedAcceptedMoves: 8,
                maxRetainedValidationFailures: 8,
                maxRetainedWorkerBatches: 8,
                maxRetainedWorkerFailures: 8));

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
                messageTimestampUtcTicks: 100 + i,
                payloadOffset: i);
            payload[i] = (byte)(i + 1);
        }

        return new RadarEventBatch(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            events,
            payload);
    }

    private static RadarEventBatch CreateInvalidSourceBatch(SourceUniverseVersion sourceUniverseVersion) =>
        new(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            new[]
            {
                CreateEvent(
                    sourceId: 1,
                    messageTimestampUtcTicks: 100,
                    payloadOffset: 0)
            },
            new byte[] { 1 });

    private static RadarStreamEvent CreateEvent(
        int sourceId,
        long messageTimestampUtcTicks,
        int payloadOffset) =>
        new(
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
            gateCount: 1,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payloadOffset: payloadOffset,
            payloadLength: 1);
}
