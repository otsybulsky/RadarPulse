using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingQueuedProcessingSessionOrderedConcurrentTests
{
    [Fact]
    public async Task OrderedConcurrentDrainCommitsProcessingResultsInProviderSequenceOrder()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = CreateCore(universe);
        await using var session = new RadarProcessingQueuedProcessingSession(
            core,
            new RadarProcessingProviderQueueOptions(capacity: 4));

        await session.EnqueueAsync(CreateBatch(universe.Version, [0, 1], messageTimestampBase: 100));
        await session.EnqueueAsync(CreateBatch(universe.Version, [2], messageTimestampBase: 200));
        await session.EnqueueAsync(CreateBatch(universe.Version, [3], messageTimestampBase: 300));
        session.CompleteAdding();

        var result = await session.DrainOrderedConcurrentAsync(
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 2));

        Assert.True(result.IsCompleted);
        Assert.Equal([0L, 1L, 2L], result.ProcessingResults.Select(static item => item.Sequence.Value).ToArray());
        Assert.All(result.ProcessingResults, static item => Assert.True(item.IsSuccessful));
        Assert.Equal(3, result.ProcessingResults[^1].ProcessingResult?.Metrics.ProcessedBatchCount);
        Assert.Equal(4, result.ProcessingResults[^1].ProcessingResult?.Metrics.ProcessedStreamEventCount);
        Assert.Equal(3, result.Telemetry.CompletedBatchCount);
        Assert.Equal(3, result.Telemetry.DequeuedBatchCount);
        Assert.Equal(0, result.Telemetry.FailedBatchCount);
    }

    [Fact]
    public async Task OrderedConcurrentDrainUsesAsyncWorkerTelemetryForAsyncCore()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.AsyncShardTransport,
                partitionCount: 4,
                shardCount: 2,
                asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 4)));
        await using var session = new RadarProcessingQueuedProcessingSession(
            core,
            new RadarProcessingProviderQueueOptions(capacity: 4));

        await session.EnqueueAsync(CreateBatch(universe.Version, [0, 1], messageTimestampBase: 100));
        await session.EnqueueAsync(CreateBatch(universe.Version, [2, 3], messageTimestampBase: 200));
        session.CompleteAdding();

        var result = await session.DrainOrderedConcurrentAsync(
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 2));

        Assert.True(result.IsCompleted);
        Assert.Equal(2, result.Telemetry.CompletedBatchCount);
        Assert.All(result.ProcessingResults, static processing =>
        {
            Assert.True(processing.IsSuccessful);
            Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, processing.ProcessingResult?.ExecutionMode);
            Assert.NotNull(processing.ProcessingResult?.WorkerTelemetry);
            Assert.Equal(2, processing.ProcessingResult?.WorkerTelemetry?.WorkerCount);
            Assert.Equal(4, processing.ProcessingResult?.WorkerTelemetry?.QueueCapacity);
            Assert.Equal(0, processing.ProcessingResult?.WorkerTelemetry?.Counters.RejectedDispatchCount);
            Assert.Equal(0, processing.ProcessingResult?.WorkerTelemetry?.Counters.FailedBatchCount);
        });
    }

    [Fact]
    public async Task OrderedConcurrentDrainFailsClosedAndSkipsLaterActiveSuccess()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreateCore(universe);
        await using var session = new RadarProcessingQueuedProcessingSession(
            core,
            new RadarProcessingProviderQueueOptions(capacity: 2));

        await session.EnqueueAsync(CreateInvalidSourceBatch(universe.Version));
        await session.EnqueueAsync(CreateBatch(universe.Version, [0], messageTimestampBase: 100));
        session.CompleteAdding();

        var result = await session.DrainOrderedConcurrentAsync(
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 2));

        Assert.True(result.IsFaulted);
        Assert.Equal(
            [
                RadarProcessingQueuedBatchProcessingStatus.FailedValidation,
                RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault
            ],
            result.ProcessingResults.Select(static item => item.Status).ToArray());
        Assert.Equal(0, core.CreateMetrics().ProcessedBatchCount);
        Assert.Equal(1, result.Telemetry.FailedBatchCount);
        Assert.Equal(1, result.Telemetry.SkippedAfterFaultCount);
    }

    [Fact]
    public async Task OrderedConcurrentDrainValidatesCommitOrderAgainstCurrentSourceState()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreateCore(universe);
        await using var session = new RadarProcessingQueuedProcessingSession(
            core,
            new RadarProcessingProviderQueueOptions(capacity: 2));

        await session.EnqueueAsync(CreateBatch(universe.Version, [0], messageTimestampBase: 200));
        await session.EnqueueAsync(CreateBatch(universe.Version, [0], messageTimestampBase: 100));
        session.CompleteAdding();

        var result = await session.DrainOrderedConcurrentAsync(
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 2));

        Assert.True(result.IsFaulted);
        Assert.Equal(
            [
                RadarProcessingQueuedBatchProcessingStatus.Succeeded,
                RadarProcessingQueuedBatchProcessingStatus.FailedValidation
            ],
            result.ProcessingResults.Select(static item => item.Status).ToArray());
        Assert.Equal(RadarProcessingValidationError.SourceOrderViolation, result.ProcessingResults[1].ProcessingResult?.Validation.Error);
        Assert.Equal(1, core.CreateMetrics().ProcessedBatchCount);
        Assert.Equal(1, core.GetSourceSnapshot(0).ProcessedEventCount);
        Assert.Equal(200, core.GetSourceSnapshot(0).LastMessageTimestampUtcTicks);
    }

    private static RadarProcessingCore CreateCore(RadarSourceUniverse universe) =>
        new(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount: universe.SourceCount,
                shardCount: Math.Min(2, universe.SourceCount)));

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
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
