using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingDurableRebalanceSessionTests
{
    [Fact]
    public async Task DurableRebalancePreservesAcceptedMoveAgainstDirectReference()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var reference = CreateSession(universe);
        var durable = CreateSession(universe);
        var batch = CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1]);
        var referenceResult = reference.Process(batch);
        var queue = new RadarProcessingDurableEnvelopeQueue();
        await using var session = new RadarProcessingDurableRebalanceSession(durable, queue);

        queue.Accept(BatchId("batch-0"), batch);

        var result = await session.DrainAsync();

        Assert.True(result.IsCompleted);
        Assert.Equal(reference.CurrentTopology.Version, durable.CurrentTopology.Version);
        Assert.Equal(reference.CurrentTopology.Version, result.FinalTopologyVersion);
        var processing = Assert.Single(result.ProcessingResults);
        Assert.True(processing.IsSuccessful);
        Assert.True(processing.RebalanceResult?.Validation.IsValid);
        Assert.Equal(referenceResult.PublishedMigration, processing.RebalanceResult?.PublishedMigration);
        Assert.Equal(
            referenceResult.TelemetrySummary.Counters.AcceptedMoveCount,
            processing.RebalanceResult?.TelemetrySummary.Counters.AcceptedMoveCount);
        Assert.Equal(1, result.QueueSummary.ReleasedEnvelopeCount);
        Assert.False(result.QueueSummary.HasBlockingEnvelope);
    }

    [Fact]
    public async Task DurableRebalanceRecomputesLaterCompletedDeltaAfterTopologyMove()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var reference = CreateSession(universe);
        var durable = CreateSession(universe);
        var batches = new[]
        {
            CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1]),
            CreateEmptyBatch(universe.Version)
        };
        var referenceResults = batches.Select(batch => reference.Process(batch)).ToArray();
        var queue = new RadarProcessingDurableEnvelopeQueue();
        await using var session = new RadarProcessingDurableRebalanceSession(durable, queue);

        queue.Accept(BatchId("batch-0"), batches[0]);
        queue.Accept(BatchId("batch-1"), batches[1]);

        var first = queue.ClaimNext("worker-a").ClaimedEnvelope!;
        var second = queue.ClaimNext("worker-b").ClaimedEnvelope!;

        await session.ProcessClaimedAsync(second);
        Assert.Empty(session.CommitReady());

        await session.ProcessClaimedAsync(first);
        var published = session.CommitReady();
        var result = session.CreateResult();

        Assert.True(result.IsCompleted);
        Assert.Equal([0L, 1L], published.Select(static item => item.Sequence.Value).ToArray());
        Assert.All(published, static item => Assert.True(item.IsSuccessful));
        Assert.True(published[0].RebalanceResult?.PublishedMigration);
        Assert.Equal(reference.CurrentTopology.Version, durable.CurrentTopology.Version);
        Assert.Equal(reference.CurrentTopology.Version, result.FinalTopologyVersion);
        Assert.Equal(
            referenceResults[0].TelemetrySummary.Counters.AcceptedMoveCount,
            published[0].RebalanceResult?.TelemetrySummary.Counters.AcceptedMoveCount);

        var secondProcessing = published[1].ProcessingResult;
        var secondTelemetry = Assert.IsType<RadarProcessingTelemetry>(secondProcessing?.Telemetry);
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), secondProcessing?.TopologyVersion);
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), secondTelemetry.TopologyVersion);
        Assert.Equal(referenceResults[1].ProcessingResult.TopologyVersion, secondProcessing?.TopologyVersion);
        Assert.Equal(2, result.QueueSummary.ReleasedEnvelopeCount);
        Assert.False(result.QueueSummary.HasBlockingEnvelope);
    }

    [Fact]
    public async Task DurableRebalanceFailureBlocksLaterPublication()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var queue = new RadarProcessingDurableEnvelopeQueue();
        await using var session = new RadarProcessingDurableRebalanceSession(CreateSession(universe), queue);

        queue.Accept(BatchId("invalid"), CreateInvalidSourceBatch(universe.Version));
        queue.Accept(BatchId("valid"), CreateEmptyBatch(universe.Version));

        var invalid = queue.ClaimNext("worker-a").ClaimedEnvelope!;
        var valid = queue.ClaimNext("worker-b").ClaimedEnvelope!;

        await session.ProcessClaimedAsync(valid);
        Assert.Empty(session.CommitReady());

        await session.ProcessClaimedAsync(invalid);
        var published = session.CommitReady();
        var result = session.CreateResult();

        var failure = Assert.Single(published);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.FailedValidation, failure.Status);
        Assert.True(result.IsFaulted);
        Assert.Equal(1, result.QueueSummary.CompletedEnvelopeCount);
        Assert.Equal(1, result.QueueSummary.FailedEnvelopeCount);
        Assert.Equal(RadarProcessingDurableEnvelopeState.Failed, result.QueueSummary.FirstBlockingState);
    }

    [Fact]
    public async Task DurableAsyncRebalancePreservesWorkerTelemetry()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var queue = new RadarProcessingDurableEnvelopeQueue();
        await using var session = new RadarProcessingDurableRebalanceSession(
            CreateSession(
                universe,
                RadarProcessingExecutionMode.AsyncShardTransport,
                asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 4)),
            queue);

        queue.Accept(BatchId("batch-0"), CreateEmptyBatch(universe.Version));
        queue.Accept(BatchId("batch-1"), CreateEmptyBatch(universe.Version));

        var result = await session.DrainAsync();

        Assert.True(result.IsCompleted);
        Assert.Equal(2, result.QueueSummary.ReleasedEnvelopeCount);
        Assert.All(result.ProcessingResults, static processing =>
        {
            Assert.True(processing.IsSuccessful);
            Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, processing.ProcessingResult?.ExecutionMode);
            Assert.NotNull(processing.RebalanceResult?.WorkerTelemetry);
            Assert.Equal(2, processing.RebalanceResult?.WorkerTelemetry?.WorkerCount);
            Assert.Equal(4, processing.RebalanceResult?.WorkerTelemetry?.QueueCapacity);
            Assert.Equal(0, processing.RebalanceResult?.WorkerTelemetry?.Counters.FailedBatchCount);
        });
    }

    private static RadarProcessingDurableBatchId BatchId(
        string value) =>
        new(value);

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

    private static RadarSourceUniverse CreateUniverse(
        int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarEventBatch CreateEmptyBatch(
        SourceUniverseVersion sourceUniverseVersion) =>
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

    private static RadarEventBatch CreateInvalidSourceBatch(
        SourceUniverseVersion sourceUniverseVersion) =>
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
