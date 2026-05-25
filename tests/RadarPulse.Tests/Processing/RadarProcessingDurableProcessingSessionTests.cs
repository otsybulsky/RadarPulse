using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingDurableProcessingSessionTests
{
    [Fact]
    public async Task OutOfOrderWorkerCompletionCommitsInProviderSequence()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = CreateCore(universe);
        var queue = new RadarProcessingDurableEnvelopeQueue();
        await using var session = new RadarProcessingDurableProcessingSession(core, queue);

        queue.Accept(BatchId("batch-0"), CreateBatch(universe.Version, [0], messageTimestampBase: 100));
        queue.Accept(BatchId("batch-1"), CreateBatch(universe.Version, [1], messageTimestampBase: 200));
        queue.Accept(BatchId("batch-2"), CreateBatch(universe.Version, [2], messageTimestampBase: 300));

        var first = queue.ClaimNext("worker-a").ClaimedEnvelope!;
        var second = queue.ClaimNext("worker-b").ClaimedEnvelope!;
        var third = queue.ClaimNext("worker-c").ClaimedEnvelope!;

        await session.ProcessClaimedAsync(second);
        Assert.Empty(session.CommitReady());
        Assert.Equal(0, core.CreateMetrics().ProcessedBatchCount);

        await session.ProcessClaimedAsync(first);
        var firstPublished = session.CommitReady();

        Assert.Equal([0L, 1L], firstPublished.Select(static item => item.Sequence.Value).ToArray());
        Assert.All(firstPublished, static item => Assert.True(item.IsSuccessful));
        Assert.Equal(2, core.CreateMetrics().ProcessedBatchCount);

        await session.ProcessClaimedAsync(third);
        var secondPublished = session.CommitReady();
        var result = session.CreateResult();

        Assert.Equal([2L], secondPublished.Select(static item => item.Sequence.Value).ToArray());
        Assert.True(result.IsCompleted);
        Assert.Equal([0L, 1L, 2L], result.ProcessingResults.Select(static item => item.Sequence.Value).ToArray());
        Assert.Equal(3, result.QueueSummary.ReleasedEnvelopeCount);
        Assert.False(result.QueueSummary.HasBlockingEnvelope);
    }

    [Fact]
    public async Task LaterCompletedEnvelopeWaitsBehindEarlierClaimedEnvelope()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var queue = new RadarProcessingDurableEnvelopeQueue();
        await using var session = new RadarProcessingDurableProcessingSession(CreateCore(universe), queue);

        queue.Accept(BatchId("batch-0"), CreateBatch(universe.Version, [0], messageTimestampBase: 100));
        queue.Accept(BatchId("batch-1"), CreateBatch(universe.Version, [1], messageTimestampBase: 200));

        var first = queue.ClaimNext("worker-a").ClaimedEnvelope!;
        var second = queue.ClaimNext("worker-b").ClaimedEnvelope!;

        await session.ProcessClaimedAsync(second);
        var published = session.CommitReady();
        var summary = queue.CreateSummary();

        Assert.Empty(published);
        Assert.Equal(RadarProcessingDurableEnvelopeState.Claimed, summary.FirstBlockingState);
        Assert.Equal(first.BatchId, summary.FirstBlockingBatchId!.Value);
        Assert.Equal(0, summary.FirstBlockingSequence!.Value.Value);
        Assert.Equal(1, summary.CompletedEnvelopeCount);
        Assert.Equal(1, summary.ClaimedEnvelopeCount);
    }

    [Fact]
    public async Task EarlierFailureBlocksLaterPublication()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var queue = new RadarProcessingDurableEnvelopeQueue();
        await using var session = new RadarProcessingDurableProcessingSession(CreateCore(universe), queue);

        queue.Accept(BatchId("invalid"), CreateInvalidSourceBatch(universe.Version));
        queue.Accept(BatchId("valid"), CreateBatch(universe.Version, [0], messageTimestampBase: 100));

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
        Assert.Equal([RadarProcessingQueuedBatchProcessingStatus.FailedValidation], result.ProcessingResults.Select(static item => item.Status).ToArray());
        Assert.Equal(0, result.QueueSummary.OldestUncommittedSequence!.Value.Value);
        Assert.Equal(RadarProcessingDurableEnvelopeState.Failed, result.QueueSummary.FirstBlockingState);
        Assert.Equal(1, result.QueueSummary.CompletedEnvelopeCount);
        Assert.Equal(1, result.QueueSummary.FailedEnvelopeCount);
        Assert.Equal(0, universe.SourceCount == 0 ? 0 : session.Core.CreateMetrics().ProcessedBatchCount);
    }

    [Fact]
    public async Task CommitValidatesSourceOrderAgainstCurrentState()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var queue = new RadarProcessingDurableEnvelopeQueue();
        var core = CreateCore(universe);
        await using var session = new RadarProcessingDurableProcessingSession(core, queue);

        queue.Accept(BatchId("newer"), CreateBatch(universe.Version, [0], messageTimestampBase: 200));
        queue.Accept(BatchId("older"), CreateBatch(universe.Version, [0], messageTimestampBase: 100));

        var newer = queue.ClaimNext("worker-a").ClaimedEnvelope!;
        var older = queue.ClaimNext("worker-b").ClaimedEnvelope!;

        await session.ProcessClaimedAsync(older);
        await session.ProcessClaimedAsync(newer);
        var published = session.CommitReady();
        var result = session.CreateResult();

        Assert.Equal(
            [
                RadarProcessingQueuedBatchProcessingStatus.Succeeded,
                RadarProcessingQueuedBatchProcessingStatus.FailedValidation
            ],
            published.Select(static item => item.Status).ToArray());
        Assert.True(result.IsFaulted);
        Assert.Equal(RadarProcessingValidationError.SourceOrderViolation, published[1].ProcessingResult?.Validation.Error);
        Assert.Equal(1, core.CreateMetrics().ProcessedBatchCount);
        Assert.Equal(200, core.GetSourceSnapshot(0).LastMessageTimestampUtcTicks);
        Assert.Equal(1, result.QueueSummary.ReleasedEnvelopeCount);
        Assert.Equal(1, result.QueueSummary.FailedEnvelopeCount);
    }

    [Fact]
    public async Task DrainProcessesPendingEnvelopes()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var queue = new RadarProcessingDurableEnvelopeQueue();
        await using var session = new RadarProcessingDurableProcessingSession(CreateCore(universe), queue);

        queue.Accept(BatchId("batch-0"), CreateBatch(universe.Version, [0], messageTimestampBase: 100));
        queue.Accept(BatchId("batch-1"), CreateBatch(universe.Version, [1], messageTimestampBase: 200));

        var result = await session.DrainAsync();

        Assert.True(result.IsCompleted);
        Assert.Equal([0L, 1L], result.ProcessingResults.Select(static item => item.Sequence.Value).ToArray());
        Assert.All(result.ProcessingResults, static item => Assert.True(item.IsSuccessful));
        Assert.Equal(2, result.QueueSummary.ReleasedEnvelopeCount);
        Assert.False(result.QueueSummary.HasUncommittedEnvelope);
    }

    [Fact]
    public async Task AsyncCoreUsesWorkerTelemetryThroughDurableCommit()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.AsyncShardTransport,
                partitionCount: 4,
                shardCount: 2,
                asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 4)));
        var queue = new RadarProcessingDurableEnvelopeQueue();
        await using var session = new RadarProcessingDurableProcessingSession(core, queue);

        queue.Accept(BatchId("batch-0"), CreateBatch(universe.Version, [0, 1], messageTimestampBase: 100));
        queue.Accept(BatchId("batch-1"), CreateBatch(universe.Version, [2, 3], messageTimestampBase: 200));

        var result = await session.DrainAsync();

        Assert.True(result.IsCompleted);
        Assert.Equal(2, result.QueueSummary.ReleasedEnvelopeCount);
        Assert.All(result.ProcessingResults, static processing =>
        {
            Assert.True(processing.IsSuccessful);
            Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, processing.ProcessingResult?.ExecutionMode);
            Assert.NotNull(processing.ProcessingResult?.WorkerTelemetry);
            Assert.Equal(2, processing.ProcessingResult?.WorkerTelemetry?.WorkerCount);
            Assert.Equal(4, processing.ProcessingResult?.WorkerTelemetry?.QueueCapacity);
            Assert.Equal(0, processing.ProcessingResult?.WorkerTelemetry?.Counters.FailedBatchCount);
        });
    }

    private static RadarProcessingDurableBatchId BatchId(
        string value) =>
        new(value);

    private static RadarProcessingCore CreateCore(
        RadarSourceUniverse universe) =>
        new(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount: universe.SourceCount,
                shardCount: Math.Min(2, universe.SourceCount)));

    private static RadarSourceUniverse CreateUniverse(
        int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarEventBatch CreateBatch(
        SourceUniverseVersion sourceUniverseVersion,
        int[] sourceIds,
        long messageTimestampBase)
    {
        var events = new RadarStreamEvent[sourceIds.Length];
        var payload = new byte[sourceIds.Length];

        for (var i = 0; i < sourceIds.Length; i++)
        {
            events[i] = new RadarStreamEvent(
                sourceIds[i],
                radarOrdinal: 0,
                volumeTimestampUtcTicks: 90,
                messageTimestampUtcTicks: messageTimestampBase + i,
                sourceRecord: 1,
                sourceMessage: 1,
                radialSequence: i,
                elevationSlot: 0,
                azimuthBucket: (ushort)sourceIds[i],
                rangeBand: 0,
                momentId: 0,
                gateStart: 0,
                gateCount: 1,
                wordSize: RadarStreamWordSize.EightBit,
                scale: 1.0f,
                offset: 0.0f,
                statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                payloadOffset: i,
                payloadLength: 1);
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
                new RadarStreamEvent(
                    sourceId: 99,
                    radarOrdinal: 0,
                    volumeTimestampUtcTicks: 90,
                    messageTimestampUtcTicks: 100,
                    sourceRecord: 1,
                    sourceMessage: 1,
                    radialSequence: 0,
                    elevationSlot: 0,
                    azimuthBucket: 0,
                    rangeBand: 0,
                    momentId: 0,
                    gateStart: 0,
                    gateCount: 1,
                    wordSize: RadarStreamWordSize.EightBit,
                    scale: 1.0f,
                    offset: 0.0f,
                    statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                    payloadOffset: 0,
                    payloadLength: 1)
            },
            new byte[] { 1 });
}
