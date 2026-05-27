using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingAsyncCoreSessionTests
{
    [Fact]
    public async Task AsyncCoreSessionMatchesSynchronousPartitionedMetricsAndSnapshots()
    {
        var universe = CreateUniverse(sourceCount: 6);
        var partitioned = CreateCore(
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
        var asyncBefore = asyncCore.CreateSourceSnapshots();
        var asyncPreviousMetrics = asyncCore.CreateMetrics();

        var partitionedResult = partitioned.Process(batch);
        await using var session = new RadarProcessingAsyncCoreSession(asyncCore);
        var asyncResult = await session.ProcessAsync(batch);
        var asyncValidation = RadarProcessingOutputValidator.Validate(
            batch,
            asyncResult,
            asyncBefore,
            asyncCore.CreateSourceSnapshots(),
            asyncPreviousMetrics);

        Assert.True(partitionedResult.IsValid);
        Assert.True(asyncResult.IsValid);
        Assert.True(asyncValidation.IsValid);
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, asyncResult.ExecutionMode);
        Assert.Equal(partitionedResult.Metrics, asyncResult.Metrics);
        Assert.Equal(partitioned.CreateSourceSnapshots(), asyncCore.CreateSourceSnapshots());
        Assert.NotNull(asyncResult.Telemetry);
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, asyncResult.Telemetry.ExecutionMode);
        Assert.Equal(partitionedResult.Telemetry?.BatchMetrics, asyncResult.Telemetry.BatchMetrics);
        Assert.NotNull(asyncResult.WorkerTelemetry);
        Assert.Equal(3, asyncResult.WorkerTelemetry.WorkerCount);
        Assert.Equal(1, asyncResult.WorkerTelemetry.QueueCapacity);
        Assert.Equal(1, asyncResult.WorkerTelemetry.Counters.DispatchedBatchCount);
        Assert.Equal(1, asyncResult.WorkerTelemetry.Counters.CompletedBatchCount);
        Assert.Equal(3, asyncResult.WorkerTelemetry.Counters.SubmittedWorkItemCount);
        Assert.Equal(3, asyncResult.WorkerTelemetry.Counters.SucceededWorkItemCount);
        Assert.Single(asyncResult.WorkerTelemetry.RecentBatches);
        Assert.Empty(asyncResult.WorkerTelemetry.RecentFailures);
    }

    [Fact]
    public async Task AsyncCoreSessionPreservesOwnedAndLeasedBorrowedBatchParity()
    {
        var universe = CreateUniverse(sourceCount: 6);
        var ownedCore = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 6,
            shardCount: 3,
            asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 3, queueCapacity: 1));
        var leasedCore = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 6,
            shardCount: 3,
            asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 3, queueCapacity: 1));
        var ownedBatch = CreateMixedBatchBuilder(universe.Version).Build();

        await using var ownedSession = new RadarProcessingAsyncCoreSession(ownedCore);
        await using var leasedSession = new RadarProcessingAsyncCoreSession(leasedCore);
        var ownedResult = await ownedSession.ProcessAsync(ownedBatch);
        RadarProcessingResult? leasedResult = null;
        CreateMixedBatchBuilder(universe.Version).ConsumeLeased(batch =>
        {
            Assert.Equal(RadarEventBatchLifetime.Leased, batch.Lifetime);
            leasedResult = leasedSession.ProcessAsync(batch).AsTask().GetAwaiter().GetResult();
        });

        Assert.NotNull(leasedResult);
        Assert.Equal(ownedResult.Metrics, leasedResult.Metrics);
        Assert.Equal(ownedCore.CreateSourceSnapshots(), leasedCore.CreateSourceSnapshots());
    }

    [Fact]
    public async Task AsyncCoreSessionRejectsCapacityTooSmallWithoutStateMutationAndReportsWorkerTelemetry()
    {
        var universe = CreateUniverse(sourceCount: 6);
        var core = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 6,
            shardCount: 3,
            asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 1, queueCapacity: 1));
        var batch = CreateMixedBatch(universe.Version);

        await using var session = new RadarProcessingAsyncCoreSession(core);
        var result = await session.ProcessAsync(batch);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.MetricsMismatch, result.Validation.Error);
        Assert.Equal(RadarProcessingMetrics.Empty, result.Metrics);
        Assert.Equal(RadarProcessingMetrics.Empty, core.CreateMetrics());
        Assert.NotNull(result.WorkerTelemetry);
        Assert.Equal(1, result.WorkerTelemetry.Counters.DispatchedBatchCount);
        Assert.Equal(1, result.WorkerTelemetry.Counters.FailedBatchCount);
        Assert.Equal(1, result.WorkerTelemetry.Counters.RejectedDispatchCount);
        Assert.Equal(0, result.WorkerTelemetry.Counters.AcceptedWorkItemCount);
        Assert.Single(result.WorkerTelemetry.RecentFailures);
        Assert.Equal(RadarProcessingAsyncFailureKind.EnqueueRejected, result.WorkerTelemetry.RecentFailures[0].FailureKind);
    }

    [Fact]
    public async Task AsyncCoreSessionReportsSourceOrderViolationWithoutCountingBatchComplete()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var core = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 2,
            shardCount: 1);
        var batch = CreateBatch(
            universe.Version,
            new[]
            {
                CreateEvent(sourceId: 1, messageTimestampUtcTicks: 100, payloadOffset: 0),
                CreateEvent(sourceId: 1, messageTimestampUtcTicks: 99, payloadOffset: 4)
            },
            new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        await using var session = new RadarProcessingAsyncCoreSession(core);
        var result = await session.ProcessAsync(batch);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.SourceOrderViolation, result.Validation.Error);
        Assert.Equal(1, result.Validation.SourceId);
        Assert.Equal(1, result.Validation.EventIndex);
        Assert.Equal(0, result.Metrics.ProcessedBatchCount);
        Assert.Equal(1, result.Metrics.ProcessedStreamEventCount);
        Assert.NotNull(result.WorkerTelemetry);
        Assert.Equal(1, result.WorkerTelemetry.Counters.FailedWorkItemCount);
    }

    [Fact]
    public async Task AsyncCoreSessionOwnsAndDisposesDefaultWorkerGroup()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 1,
            shardCount: 1);
        var session = new RadarProcessingAsyncCoreSession(core);

        await session.ProcessAsync(CreateEmptyBatch(universe.Version));
        await session.DisposeAsync();

        Assert.Equal(RadarProcessingWorkerGroupState.Disposed, session.WorkerGroup.Status.State);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await session.ProcessAsync(CreateEmptyBatch(universe.Version)));
    }

    [Fact]
    public void AsyncCoreSessionRejectsNonAsyncCore()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var partitioned = CreateCore(
            universe,
            RadarProcessingExecutionMode.PartitionedBarrier,
            partitionCount: 1,
            shardCount: 1);

        Assert.Throws<ArgumentNullException>(() => new RadarProcessingAsyncCoreSession(null!));
        Assert.Throws<ArgumentException>(() => new RadarProcessingAsyncCoreSession(partitioned));
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

    private static RadarEventBatch CreateEmptyBatch(
        SourceUniverseVersion sourceUniverseVersion) =>
        CreateBatch(
            sourceUniverseVersion,
            Array.Empty<RadarStreamEvent>(),
            Array.Empty<byte>());

    private static RadarEventBatch CreateMixedBatch(
        SourceUniverseVersion sourceUniverseVersion) =>
        CreateMixedBatchBuilder(sourceUniverseVersion).Build();

    private static RadarEventBatchBuilder CreateMixedBatchBuilder(
        SourceUniverseVersion sourceUniverseVersion)
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 6, initialPayloadCapacity: 24);
        AddEvent(builder, sourceUniverseVersion, sourceId: 0, messageTimestampUtcTicks: 100, payload: new byte[] { 1, 2, 3, 4 });
        AddEvent(builder, sourceUniverseVersion, sourceId: 3, messageTimestampUtcTicks: 101, payload: new byte[] { 0, 5, 1, 0 },
            wordSize: RadarStreamWordSize.SixteenBit);
        AddEvent(builder, sourceUniverseVersion, sourceId: 1, messageTimestampUtcTicks: 102, payload: new byte[] { 5, 6, 7, 8 });
        AddEvent(builder, sourceUniverseVersion, sourceId: 5, messageTimestampUtcTicks: 103, payload: new byte[] { 2, 0, 0, 1 },
            wordSize: RadarStreamWordSize.SixteenBit);
        AddEvent(builder, sourceUniverseVersion, sourceId: 3, messageTimestampUtcTicks: 104, payload: new byte[] { 9, 10, 11, 12 });
        return builder;
    }

    private static RadarEventBatch CreateBatch(
        SourceUniverseVersion sourceUniverseVersion,
        RadarStreamEvent[] events,
        byte[] payload) =>
        new(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            events,
            payload);

    private static void AddEvent(
        RadarEventBatchBuilder builder,
        SourceUniverseVersion sourceUniverseVersion,
        int sourceId,
        long messageTimestampUtcTicks,
        byte[] payload,
        RadarStreamWordSize wordSize = RadarStreamWordSize.EightBit)
    {
        builder.AddEvent(
            CreateIdentity(sourceUniverseVersion, sourceId),
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks: messageTimestampUtcTicks,
            sourceRecord: 1,
            sourceMessage: 1,
            radialSequence: 1,
            gateStart: 0,
            gateCount: (ushort)(wordSize == RadarStreamWordSize.EightBit
                ? payload.Length
                : payload.Length / sizeof(ushort)),
            wordSize: wordSize,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: payload);
    }

    private static RadarStreamIdentity CreateIdentity(
        SourceUniverseVersion sourceUniverseVersion,
        int sourceId) =>
        new(
            sourceId,
            radarOrdinal: 0,
            momentId: 0,
            elevationSlot: 0,
            azimuthBucket: (ushort)sourceId,
            rangeBand: 0,
            dictionaryVersion: DictionaryVersion.Initial,
            sourceUniverseVersion: sourceUniverseVersion);

    private static RadarStreamEvent CreateEvent(
        int sourceId,
        long messageTimestampUtcTicks,
        int payloadOffset,
        ushort gateCount = 4,
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
            radialSequence: 1,
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
