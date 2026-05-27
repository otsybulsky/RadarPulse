using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests
{
    [Fact]
    public async Task LiveAdapterShapeCompletesThroughDefaultBaseline()
    {
        var universe = CreateUniverse(sourceCount: 8);
        var session = RadarProcessingRuntimeArchiveBaseline.CreateRebalanceSession(
            universe,
            partitionCount: 8,
            shardCount: 4);
        var adapter = new DeterministicArchiveLiveAdapter(
            universe,
            [
                CreateEightBitBatch(universe.Version, [0, 1, 2, 3], messageTimestampBase: 100),
                CreateEightBitBatch(universe.Version, [4, 5, 6, 7], messageTimestampBase: 200),
                CreateEightBitBatch(universe.Version, [0, 2, 4, 6], messageTimestampBase: 300)
            ]);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();

        var result = await runner.RunRebalanceAsync(adapter.PublishTo, session);

        Assert.True(result.IsCompleted);
        Assert.True(result.HasRetainedPayloadPrewarm);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.OverlapTelemetry.RetentionStrategy);
        Assert.Equal(3, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(0, result.ProviderResult.RejectedPublishCount);
        Assert.Equal(3, result.QueueTelemetry.EnqueuedBatchCount);
        Assert.Equal(3, result.QueueTelemetry.DequeuedBatchCount);
        Assert.Equal(3, result.QueueTelemetry.CompletedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.FailedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CanceledBatchCount);
        Assert.Equal(0, result.QueueTelemetry.SkippedAfterFaultCount);
        Assert.Equal(0, result.ProviderResult.RetentionTelemetry.ReleaseFailedCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);

        Assert.Equal(3, result.Consumer.SessionResult.ProcessingResults.Count);
        Assert.All(result.Consumer.SessionResult.ProcessingResults, static processing =>
        {
            Assert.True(processing.IsSuccessful);
            Assert.True(processing.RebalanceResult?.Validation.IsValid);
            Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, processing.ProcessingResult?.ExecutionMode);

            var workerCounters = processing.RebalanceResult?.WorkerTelemetry?.Counters;
            Assert.NotNull(workerCounters);
            Assert.Equal(0, workerCounters.FailedBatchCount);
            Assert.Equal(0, workerCounters.CanceledBatchCount);
            Assert.Equal(0, workerCounters.TimedOutBatchCount);
            Assert.Equal(0, workerCounters.RejectedDispatchCount);
            Assert.Equal(0, workerCounters.FailedWorkItemCount);
            Assert.Equal(0, workerCounters.CanceledWorkItemCount);
        });

        var lastProcessing = result.Consumer.SessionResult.ProcessingResults[^1];
        Assert.Equal(3, lastProcessing.ProcessingResult?.Metrics.ProcessedBatchCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount,
            lastProcessing.RebalanceResult?.WorkerTelemetry?.WorkerCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity,
            lastProcessing.RebalanceResult?.WorkerTelemetry?.QueueCapacity);
    }

    [Fact]
    public async Task LiveAdapterShapeCompletesThroughOrderedConcurrentProcessingDefaultBaseline()
    {
        var universe = CreateUniverse(sourceCount: 8);
        var core = RadarProcessingRuntimeArchiveBaseline.CreateCore(
            universe,
            partitionCount: 8,
            shardCount: 4);
        var adapter = new DeterministicArchiveLiveAdapter(
            universe,
            [
                CreateEightBitBatch(universe.Version, [0, 1, 2, 3], messageTimestampBase: 100),
                CreateEightBitBatch(universe.Version, [4, 5, 6, 7], messageTimestampBase: 200),
                CreateEightBitBatch(universe.Version, [0, 2, 4, 6], messageTimestampBase: 300)
            ]);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();

        var result = await runner.RunProcessingAsync(adapter.PublishTo, core);

        Assert.True(result.IsCompleted);
        Assert.True(result.HasRetainedPayloadPrewarm);
        Assert.Equal(
            RadarProcessingRuntimeArchiveBaseline.OrderedActiveBatchCapacity,
            result.RetainedPayloadPrewarm.RetainedBatchCount);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.OverlapTelemetry.RetentionStrategy);
        Assert.Equal(3, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(3, result.QueueTelemetry.EnqueuedBatchCount);
        Assert.Equal(3, result.QueueTelemetry.DequeuedBatchCount);
        Assert.Equal(3, result.QueueTelemetry.CompletedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.FailedBatchCount);
        Assert.Equal(0, result.ProviderResult.RetentionTelemetry.ReleaseFailedCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);

        Assert.Equal(3, result.Consumer.SessionResult.ProcessingResults.Count);
        Assert.Equal([0L, 1L, 2L], result.Consumer.SessionResult.ProcessingResults
            .Select(static processing => processing.Sequence.Value)
            .ToArray());
        Assert.All(result.Consumer.SessionResult.ProcessingResults, static processing =>
        {
            Assert.True(processing.IsSuccessful);
            Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, processing.ProcessingResult?.ExecutionMode);
            Assert.NotNull(processing.ProcessingResult?.WorkerTelemetry);
            Assert.Equal(
                RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount,
                processing.ProcessingResult?.WorkerTelemetry?.WorkerCount);
            Assert.Equal(
                RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity,
                processing.ProcessingResult?.WorkerTelemetry?.QueueCapacity);
            Assert.Equal(0, processing.ProcessingResult?.WorkerTelemetry?.Counters.RejectedDispatchCount);
        });
        Assert.Equal(3, result.Consumer.SessionResult.ProcessingResults[^1].ProcessingResult?.Metrics.ProcessedBatchCount);
    }

    [Fact]
    public async Task LiveAdapterShapeCompletesThroughOrderedConcurrentRebalanceDefaultBaseline()
    {
        var universe = CreateUniverse(sourceCount: 8);
        var session = RadarProcessingRuntimeArchiveBaseline.CreateRebalanceSession(
            universe,
            partitionCount: 8,
            shardCount: 4);
        var adapter = new DeterministicArchiveLiveAdapter(
            universe,
            [
                CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1], messageTimestampBase: 100),
                CreateEightBitBatch(universe.Version, [2, 3, 4, 5], messageTimestampBase: 200),
                CreateEmptyBatch(universe.Version)
            ]);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();

        var result = await runner.RunOrderedRebalanceAsync(adapter.PublishTo, session);

        Assert.True(result.IsCompleted);
        Assert.True(result.HasRetainedPayloadPrewarm);
        Assert.Equal(
            RadarProcessingRuntimeArchiveBaseline.OrderedActiveBatchCapacity,
            result.RetainedPayloadPrewarm.RetainedBatchCount);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.OverlapTelemetry.RetentionStrategy);
        Assert.Equal(3, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(3, result.QueueTelemetry.EnqueuedBatchCount);
        Assert.Equal(3, result.QueueTelemetry.DequeuedBatchCount);
        Assert.Equal(3, result.QueueTelemetry.CompletedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.FailedBatchCount);
        Assert.Equal(0, result.ProviderResult.RetentionTelemetry.ReleaseFailedCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);

        Assert.Equal([0L, 1L, 2L], result.Consumer.SessionResult.ProcessingResults
            .Select(static processing => processing.Sequence.Value)
            .ToArray());
        Assert.All(result.Consumer.SessionResult.ProcessingResults, static processing =>
        {
            Assert.True(processing.IsSuccessful);
            Assert.True(processing.RebalanceResult?.Validation.IsValid);
            Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, processing.ProcessingResult?.ExecutionMode);
            Assert.NotNull(processing.RebalanceResult?.WorkerTelemetry);
            Assert.Equal(
                RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount,
                processing.RebalanceResult?.WorkerTelemetry?.WorkerCount);
            Assert.Equal(
                RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity,
                processing.RebalanceResult?.WorkerTelemetry?.QueueCapacity);
            Assert.Equal(0, processing.RebalanceResult?.WorkerTelemetry?.Counters.RejectedDispatchCount);
        });
        Assert.Equal(3, result.Consumer.SessionResult.ProcessingResults[^1].ProcessingResult?.Metrics.ProcessedBatchCount);
        Assert.True(result.Consumer.SessionResult.FinalTopologyVersion?.Value >= RadarProcessingTopologyVersion.Initial.Value);
    }

    [Fact]
    public async Task LiveAdapterShapeValidationFailureCleansRetainedPressureWithoutBorrowedFallback()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var session = RadarProcessingRuntimeArchiveBaseline.CreateRebalanceSession(
            universe,
            partitionCount: 4,
            shardCount: 2);
        var adapter = new DeterministicArchiveLiveAdapter(
            universe,
            [
                CreateEightBitBatch(universe.Version, [0, 1], messageTimestampBase: 100),
                CreateInvalidSourceBatch(universe.Version),
                CreateEightBitBatch(universe.Version, [2, 3], messageTimestampBase: 300)
            ]);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();

        var result = await runner.RunRebalanceAsync(adapter.PublishTo, session);

        Assert.Equal(RadarProcessingArchiveQueuedOverlapStatus.ConsumerFaulted, result.Status);
        Assert.True(result.IsFaulted);
        Assert.True(result.Producer.IsCompleted);
        Assert.True(result.Consumer.IsFaulted);
        Assert.Equal(3, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(0, result.ProviderResult.RejectedPublishCount);
        Assert.Equal(0, result.ProviderResult.RetentionTelemetry.ReleaseFailedCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);

        Assert.Equal(3, result.Consumer.SessionResult.ProcessingResults.Count);
        Assert.Equal(
            [
                RadarProcessingQueuedBatchProcessingStatus.Succeeded,
                RadarProcessingQueuedBatchProcessingStatus.FailedValidation,
                RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault
            ],
            result.Consumer.SessionResult.ProcessingResults
                .Select(static processing => processing.Status)
                .ToArray());
        Assert.Equal(1, result.QueueTelemetry.CompletedBatchCount);
        Assert.Equal(1, result.QueueTelemetry.FailedBatchCount);
        Assert.Equal(1, result.QueueTelemetry.SkippedAfterFaultCount);
        Assert.Equal(
            RadarProcessingValidationError.SourceIdOutsideUniverse,
            result.Consumer.SessionResult.ProcessingResults[1].ProcessingResult?.Validation.Error);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.OverlapTelemetry.RetentionStrategy);
    }

    [Fact]
    public async Task OrderedConcurrentProcessingValidationFailureCleansRetainedPressureWithoutBorrowedFallback()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = RadarProcessingRuntimeArchiveBaseline.CreateCore(
            universe,
            partitionCount: 4,
            shardCount: 2);
        var adapter = new DeterministicArchiveLiveAdapter(
            universe,
            [
                CreateInvalidSourceBatch(universe.Version),
                CreateEightBitBatch(universe.Version, [0, 1], messageTimestampBase: 100)
            ]);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();

        var result = await runner.RunProcessingAsync(
            adapter.PublishTo,
            core,
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 2));

        Assert.Equal(RadarProcessingArchiveQueuedOverlapStatus.ConsumerFaulted, result.Status);
        Assert.True(result.IsFaulted);
        Assert.Equal(2, result.RetainedPayloadPrewarm.RetainedBatchCount);
        Assert.True(result.Producer.IsCompleted);
        Assert.True(result.Consumer.IsFaulted);
        Assert.Equal(2, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(0, result.ProviderResult.RejectedPublishCount);
        Assert.Equal(0, result.ProviderResult.RetentionTelemetry.ReleaseFailedCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);
        Assert.Equal(
            [
                RadarProcessingQueuedBatchProcessingStatus.FailedValidation,
                RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault
            ],
            result.Consumer.SessionResult.ProcessingResults
                .Select(static processing => processing.Status)
                .ToArray());
        Assert.Equal(0, core.CreateMetrics().ProcessedBatchCount);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.OverlapTelemetry.RetentionStrategy);
    }

    [Fact]
    public async Task OrderedConcurrentRebalanceValidationFailureCleansRetainedPressureWithoutBorrowedFallback()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var session = RadarProcessingRuntimeArchiveBaseline.CreateRebalanceSession(
            universe,
            partitionCount: 4,
            shardCount: 2);
        var adapter = new DeterministicArchiveLiveAdapter(
            universe,
            [
                CreateInvalidSourceBatch(universe.Version),
                CreateEightBitBatch(universe.Version, [0, 1], messageTimestampBase: 100)
            ]);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();

        var result = await runner.RunOrderedRebalanceAsync(
            adapter.PublishTo,
            session,
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 2));

        Assert.Equal(RadarProcessingArchiveQueuedOverlapStatus.ConsumerFaulted, result.Status);
        Assert.True(result.IsFaulted);
        Assert.Equal(2, result.RetainedPayloadPrewarm.RetainedBatchCount);
        Assert.True(result.Producer.IsCompleted);
        Assert.True(result.Consumer.IsFaulted);
        Assert.Equal(2, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(0, result.ProviderResult.RejectedPublishCount);
        Assert.Equal(0, result.ProviderResult.RetentionTelemetry.ReleaseFailedCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);
        Assert.Equal(
            [
                RadarProcessingQueuedBatchProcessingStatus.FailedValidation,
                RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault
            ],
            result.Consumer.SessionResult.ProcessingResults
                .Select(static processing => processing.Status)
                .ToArray());
        Assert.Equal(0, session.Core.CreateMetrics().ProcessedBatchCount);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.OverlapTelemetry.RetentionStrategy);
    }


    private sealed class DeterministicArchiveLiveAdapter
    {
        private readonly RadarSourceUniverse universe;
        private readonly IReadOnlyList<RadarEventBatch> batches;

        public DeterministicArchiveLiveAdapter(
            RadarSourceUniverse universe,
            IReadOnlyList<RadarEventBatch> batches)
        {
            this.universe = universe ?? throw new ArgumentNullException(nameof(universe));
            this.batches = batches ?? throw new ArgumentNullException(nameof(batches));
        }

        public ArchiveRadarEventBatchPublishResult PublishTo(
            IArchiveRadarEventBatchPublisher publisher,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(publisher);

            foreach (var batch in batches)
            {
                publisher.Publish(batch, cancellationToken);
            }

            return CreatePublishResult(universe, batches);
        }
    }

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarEventBatch CreateEightBitBatch(
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

    private static RadarEventBatch CreateEmptyBatch(
        SourceUniverseVersion sourceUniverseVersion) =>
        new(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            Array.Empty<RadarStreamEvent>(),
            Array.Empty<byte>());

    private static ArchiveRadarEventBatchPublishResult CreatePublishResult(
        RadarSourceUniverse universe,
        IReadOnlyCollection<RadarEventBatch> batches)
    {
        var normalizer = new RadarStreamIdentityNormalizer(universe);
        var eventCount = batches.Sum(static batch => batch.Events.Length);
        var payloadBytes = batches.Sum(static batch => batch.Payload.Length);
        return new ArchiveRadarEventBatchPublishResult(
            FilePath: "deterministic-live-adapter",
            Decompressor: "synthetic",
            DegreeOfParallelism: 1,
            FileSizeBytes: payloadBytes,
            CompressedRecordCount: batches.Count,
            CompressedBytes: payloadBytes,
            DecompressedBytes: payloadBytes,
            StreamSchemaVersion: StreamSchemaVersion.Current,
            DictionaryVersion: DictionaryVersion.Initial,
            SourceUniverseVersion: universe.Version,
            BatchCount: batches.Count,
            EventCount: eventCount,
            PayloadBytes: payloadBytes,
            PayloadValueCount: payloadBytes,
            RawValueChecksum: 0,
            DictionarySnapshot: normalizer.CreateDictionarySnapshot(DictionaryVersion.Initial));
    }
}
