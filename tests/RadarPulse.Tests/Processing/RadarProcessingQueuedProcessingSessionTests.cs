using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingQueuedProcessingSessionTests
{
    [Fact]
    public async Task QueuedSyncProcessingMatchesDirectCoreResult()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var directCore = CreateCore(universe, RadarProcessingExecutionMode.Sequential);
        var queuedCore = CreateCore(universe, RadarProcessingExecutionMode.Sequential);
        var batch = CreateBatch(universe.Version, sourceId: 0, messageTimestampUtcTicks: 100, firstPayloadValue: 1);

        var directResult = directCore.Process(batch);
        using var session = new RadarProcessingQueuedProcessingSession(queuedCore);
        var enqueue = await session.EnqueueAsync(batch);
        session.CompleteAdding();
        var result = await session.DrainAsync();

        Assert.True(enqueue.IsAccepted);
        Assert.True(result.IsCompleted);
        Assert.Equal(RadarProcessingQueuedSessionStatus.Completed, result.Status);
        Assert.Single(result.EnqueueResults);
        Assert.Single(result.ProcessingResults);
        Assert.True(result.ProcessingResults[0].IsSuccessful);
        Assert.Same(enqueue.Batch, result.EnqueueResults[0].Batch);
        Assert.Equal(directResult.Metrics, result.ProcessingResults[0].ProcessingResult?.Metrics);
        Assert.Equal(directCore.CreateSourceSnapshots(), queuedCore.CreateSourceSnapshots());
        Assert.Equal(1, result.Telemetry.EnqueueAttemptCount);
        Assert.Equal(1, result.Telemetry.EnqueuedBatchCount);
        Assert.Equal(1, result.Telemetry.DequeuedBatchCount);
        Assert.Equal(1, result.Telemetry.CompletedBatchCount);
        Assert.Equal(0, result.Telemetry.FailedBatchCount);
        Assert.Equal(0, result.Telemetry.CanceledBatchCount);
    }

    [Fact]
    public async Task QueuedAsyncProcessingMatchesDirectAsyncResult()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var asyncOptions = new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 1);
        var directCore = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 4,
            shardCount: 2,
            asyncOptions);
        var queuedCore = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 4,
            shardCount: 2,
            asyncOptions);
        var batch = CreateMixedBatch(universe.Version);

        await using var directSession = new RadarProcessingAsyncCoreSession(directCore);
        var directResult = await directSession.ProcessAsync(batch);
        await using var queuedSession = new RadarProcessingQueuedProcessingSession(
            queuedCore,
            new RadarProcessingProviderQueueOptions(capacity: 1));
        var enqueue = await queuedSession.EnqueueAsync(batch);
        queuedSession.CompleteAdding();
        var result = await queuedSession.DrainAsync();

        Assert.True(enqueue.IsAccepted);
        Assert.True(result.IsCompleted);
        var processing = Assert.Single(result.ProcessingResults);
        Assert.True(processing.IsSuccessful);
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, processing.ProcessingResult?.ExecutionMode);
        Assert.Equal(directResult.Metrics, processing.ProcessingResult?.Metrics);
        Assert.Equal(directCore.CreateSourceSnapshots(), queuedCore.CreateSourceSnapshots());
        Assert.NotNull(processing.ProcessingResult?.WorkerTelemetry);
        Assert.Equal(1, result.Telemetry.CompletedBatchCount);
    }

    [Fact]
    public async Task QueuedProcessingPreservesProviderSequenceOrder()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreateCore(universe, RadarProcessingExecutionMode.Sequential);
        using var session = new RadarProcessingQueuedProcessingSession(
            core,
            new RadarProcessingProviderQueueOptions(capacity: 2));

        var first = await session.EnqueueAsync(
            CreateBatch(universe.Version, sourceId: 0, messageTimestampUtcTicks: 100, firstPayloadValue: 1));
        var second = await session.EnqueueAsync(
            CreateBatch(universe.Version, sourceId: 0, messageTimestampUtcTicks: 101, firstPayloadValue: 3));
        session.CompleteAdding();

        var result = await session.DrainAsync();

        Assert.True(first.IsAccepted);
        Assert.True(second.IsAccepted);
        Assert.True(result.IsCompleted);
        Assert.Equal([0L, 1L], result.ProcessingResults.Select(static processing => processing.Sequence.Value));
        Assert.All(result.ProcessingResults, static processing => Assert.True(processing.IsSuccessful));
        Assert.Equal(2, result.ProcessingResults[^1].ProcessingResult?.Metrics.ProcessedBatchCount);
        Assert.Equal(2, result.Telemetry.CompletedBatchCount);
        Assert.Equal(2, result.Telemetry.DequeuedBatchCount);
    }

    [Fact]
    public async Task InvalidQueuedBatchFaultsSessionSkipsAcceptedRemainderAndRejectsLaterEnqueue()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreateCore(universe, RadarProcessingExecutionMode.Sequential);
        using var session = new RadarProcessingQueuedProcessingSession(
            core,
            new RadarProcessingProviderQueueOptions(capacity: 2));

        var bad = await session.EnqueueAsync(CreateInvalidSourceBatch(universe.Version));
        var acceptedBeforeFault = await session.EnqueueAsync(
            CreateBatch(universe.Version, sourceId: 0, messageTimestampUtcTicks: 100, firstPayloadValue: 1));

        var result = await session.DrainAsync();
        var rejectedAfterFault = await session.EnqueueAsync(
            CreateBatch(universe.Version, sourceId: 0, messageTimestampUtcTicks: 101, firstPayloadValue: 3));

        Assert.True(bad.IsAccepted);
        Assert.True(acceptedBeforeFault.IsAccepted);
        Assert.True(result.IsFaulted);
        Assert.Equal(RadarProcessingQueuedSessionStatus.Faulted, result.Status);
        Assert.Equal(2, result.ProcessingResults.Count);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.FailedValidation, result.ProcessingResults[0].Status);
        Assert.Equal(RadarProcessingValidationError.SourceIdOutsideUniverse, result.ProcessingResults[0].ProcessingResult?.Validation.Error);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault, result.ProcessingResults[1].Status);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Faulted, rejectedAfterFault.Status);
        Assert.Equal(1, result.Telemetry.FailedBatchCount);
        Assert.Equal(1, result.Telemetry.SkippedAfterFaultCount);
    }

    [Fact]
    public async Task ConsumerResourceLeaseFactoryWrapsDequeuedBatchesIncludingSkippedAfterFault()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var acquired = new List<long>();
        var released = new List<long>();
        using var session = new RadarProcessingQueuedProcessingSession(
            CreateCore(universe, RadarProcessingExecutionMode.Sequential),
            new RadarProcessingProviderQueueOptions(capacity: 2),
            sequence =>
            {
                acquired.Add(sequence.Value);
                return new CallbackDisposable(() => released.Add(sequence.Value));
            });

        await session.EnqueueAsync(CreateInvalidSourceBatch(universe.Version));
        await session.EnqueueAsync(
            CreateBatch(universe.Version, sourceId: 0, messageTimestampUtcTicks: 100, firstPayloadValue: 1));

        var result = await session.DrainAsync();

        Assert.True(result.IsFaulted);
        Assert.Equal(
            [RadarProcessingQueuedBatchProcessingStatus.FailedValidation, RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault],
            result.ProcessingResults.Select(static item => item.Status).ToArray());
        Assert.Equal([0L, 1L], acquired);
        Assert.Equal([0L, 1L], released);
    }

    [Fact]
    public async Task ConsumerValidationFailureReleasesActiveResource()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreateCore(
            universe,
            RadarProcessingExecutionMode.Sequential,
            handlers: [new ThrowingHandler()]);
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 1));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(
            queue,
            retainedPayloadOptions: new RadarProcessingRetainedPayloadOptions(
                RadarProcessingRetainedPayloadStrategy.PooledCopy));
        using var session = new RadarProcessingQueuedProcessingSession(
            core,
            queue,
            ownsQueue: false,
            consumerResourceLeaseFactory: publisher.AcquireConsumerResourceLease);

        PublishLeased(publisher, universe.Version, [1, 2]);
        publisher.CompleteAdding();

        var result = await session.DrainAsync();
        var provider = publisher.CreateResult();

        Assert.True(result.IsFaulted);
        var processing = Assert.Single(result.ProcessingResults);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.FailedValidation, processing.Status);
        Assert.Contains("handler failed", processing.Message, StringComparison.Ordinal);
        Assert.Equal(0, provider.Telemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, provider.Telemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, provider.Telemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(1, provider.Telemetry.ActiveRetainedBatchCountHighWatermark);
        Assert.Equal(2, provider.Telemetry.ActiveRetainedPayloadBytesHighWatermark);
        Assert.Equal(1, provider.RetentionTelemetry.ReleaseAttemptCount);
        Assert.Equal(1, provider.RetentionTelemetry.ReleasedBatchCount);
        Assert.Equal(0, provider.RetentionTelemetry.ReleaseFailedCount);
    }

    [Fact]
    public async Task DrainCancellationBeforeDequeueReturnsCanceledSessionResult()
    {
        var universe = CreateUniverse(sourceCount: 1);
        using var session = new RadarProcessingQueuedProcessingSession(
            CreateCore(universe, RadarProcessingExecutionMode.Sequential));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await session.DrainAsync(cancellation.Token);

        Assert.True(result.IsCanceled);
        Assert.Equal(RadarProcessingQueuedSessionStatus.Canceled, result.Status);
        Assert.Empty(result.ProcessingResults);
        Assert.Equal(0, result.Telemetry.CompletedBatchCount);
        Assert.Equal(0, result.Telemetry.FailedBatchCount);
    }

    [Fact]
    public async Task CancelQueuedShutdownCancelsAcceptedPendingBatchesWithoutProcessing()
    {
        var universe = CreateUniverse(sourceCount: 1);
        using var session = new RadarProcessingQueuedProcessingSession(
            CreateCore(universe, RadarProcessingExecutionMode.Sequential),
            new RadarProcessingProviderQueueOptions(
                capacity: 2,
                shutdownMode: RadarProcessingProviderQueueShutdownMode.CancelQueued));
        var first = await session.EnqueueAsync(
            CreateBatch(universe.Version, sourceId: 0, messageTimestampUtcTicks: 100, firstPayloadValue: 1));
        var second = await session.EnqueueAsync(
            CreateBatch(universe.Version, sourceId: 0, messageTimestampUtcTicks: 101, firstPayloadValue: 3));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await session.DrainAsync(cancellation.Token);

        Assert.True(first.IsAccepted);
        Assert.True(second.IsAccepted);
        Assert.True(result.IsCanceled);
        Assert.Equal(RadarProcessingQueuedSessionStatus.Canceled, result.Status);
        Assert.Equal(0, session.Queue.PendingCount);
        Assert.Equal(0, session.Queue.PendingPayloadBytes);
        Assert.Equal(
            [RadarProcessingQueuedBatchProcessingStatus.Canceled, RadarProcessingQueuedBatchProcessingStatus.Canceled],
            result.ProcessingResults.Select(static item => item.Status).ToArray());
        Assert.Equal([0L, 1L], result.ProcessingResults.Select(static item => item.Sequence.Value).ToArray());
        Assert.Equal(2, result.Telemetry.EnqueuedBatchCount);
        Assert.Equal(0, result.Telemetry.DequeuedBatchCount);
        Assert.Equal(0, result.Telemetry.CompletedBatchCount);
        Assert.Equal(2, result.Telemetry.CanceledBatchCount);
        Assert.Equal(0, result.Telemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, result.Telemetry.CurrentPendingRetainedPayloadBytes);
    }

    [Fact]
    public async Task QueuedProcessingSessionRejectsInvalidAsyncComposition()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var sequential = CreateCore(universe, RadarProcessingExecutionMode.Sequential);
        var asyncCore = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 1,
            shardCount: 1);
        var asyncSession = new RadarProcessingAsyncCoreSession(asyncCore);

        try
        {
            Assert.Throws<ArgumentException>(() =>
                new RadarProcessingQueuedProcessingSession(
                    sequential,
                    new RadarProcessingOwnedBatchQueue(),
                    asyncSession));
            Assert.Throws<ArgumentNullException>(() =>
                new RadarProcessingQueuedProcessingSession(
                    asyncCore,
                    new RadarProcessingOwnedBatchQueue(),
                    asyncCoreSession: null));
        }
        finally
        {
            await asyncSession.DisposeAsync();
        }
    }

    private static RadarProcessingCore CreateCore(
        RadarSourceUniverse universe,
        RadarProcessingExecutionMode executionMode,
        int partitionCount = 1,
        int shardCount = 1,
        RadarProcessingAsyncExecutionOptions? asyncOptions = null,
        IReadOnlyList<IRadarSourceProcessingHandler>? handlers = null) =>
        new(
            universe,
            new RadarProcessingCoreOptions(
                executionMode,
                partitionCount,
                shardCount,
                asyncExecution: asyncOptions,
                handlers: handlers));

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarEventBatch CreateBatch(
        SourceUniverseVersion sourceUniverseVersion,
        int sourceId,
        long messageTimestampUtcTicks,
        byte firstPayloadValue)
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: 2);
        AddEvent(
            builder,
            sourceUniverseVersion,
            sourceId,
            messageTimestampUtcTicks,
            [firstPayloadValue, (byte)(firstPayloadValue + 1)]);
        return builder.Build();
    }

    private static RadarEventBatch CreateMixedBatch(
        SourceUniverseVersion sourceUniverseVersion)
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 4, initialPayloadCapacity: 12);
        AddEvent(builder, sourceUniverseVersion, sourceId: 0, messageTimestampUtcTicks: 100, payload: [1, 2]);
        AddEvent(builder, sourceUniverseVersion, sourceId: 1, messageTimestampUtcTicks: 101, payload: [3, 4]);
        AddEvent(builder, sourceUniverseVersion, sourceId: 2, messageTimestampUtcTicks: 102, payload: [5, 6]);
        AddEvent(builder, sourceUniverseVersion, sourceId: 3, messageTimestampUtcTicks: 103, payload: [7, 8]);
        return builder.Build();
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
                    sourceId: 1,
                    radarOrdinal: 0,
                    volumeTimestampUtcTicks: 90,
                    messageTimestampUtcTicks: 100,
                    sourceRecord: 0,
                    sourceMessage: 0,
                    radialSequence: 0,
                    elevationSlot: 0,
                    azimuthBucket: 1,
                    rangeBand: 0,
                    momentId: 0,
                    gateStart: 0,
                    gateCount: 2,
                    wordSize: RadarStreamWordSize.EightBit,
                    scale: 1.0f,
                    offset: 0.0f,
                    statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                    payloadOffset: 0,
                    payloadLength: 2)
            },
            new byte[] { 1, 2 });

    private static void AddEvent(
        RadarEventBatchBuilder builder,
        SourceUniverseVersion sourceUniverseVersion,
        int sourceId,
        long messageTimestampUtcTicks,
        byte[] payload)
    {
        builder.AddEvent(
            new RadarStreamIdentity(
                sourceId,
                radarOrdinal: 0,
                momentId: 0,
                elevationSlot: 0,
                azimuthBucket: (ushort)sourceId,
                rangeBand: 0,
                dictionaryVersion: DictionaryVersion.Initial,
                sourceUniverseVersion: sourceUniverseVersion),
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks: messageTimestampUtcTicks,
            sourceRecord: 0,
            sourceMessage: 0,
            radialSequence: 0,
            gateStart: 0,
            gateCount: (ushort)payload.Length,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: payload);
    }

    private static void PublishLeased(
        ArchiveOwnedRadarEventBatchQueueingPublisher publisher,
        SourceUniverseVersion sourceUniverseVersion,
        byte[] payload)
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: payload.Length);
        AddEvent(
            builder,
            sourceUniverseVersion,
            sourceId: 0,
            messageTimestampUtcTicks: 100,
            payload);
        builder.ConsumeLeased(batch => publisher.Publish(batch, CancellationToken.None));
    }

    private sealed class CallbackDisposable : IDisposable
    {
        private readonly Action dispose;

        public CallbackDisposable(Action dispose)
        {
            this.dispose = dispose;
        }

        public void Dispose()
        {
            dispose();
        }
    }

    private sealed class ThrowingHandler : IRadarSourceProcessingHandler
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new("throwing", int64SlotCount: 0, doubleSlotCount: 0);

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state) =>
            throw new InvalidOperationException("handler failed");
    }
}
