using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingArchiveQueuedOverlapRunnerTests
{
    [Fact]
    public void ArchiveQueuedOverlapContractsExposeStableStatusesAndDefaults()
    {
        Assert.Equal(0, (int)RadarProcessingArchiveQueuedOverlapStatus.NotStarted);
        Assert.Equal(1, (int)RadarProcessingArchiveQueuedOverlapStatus.Completed);
        Assert.Equal(2, (int)RadarProcessingArchiveQueuedOverlapStatus.ProducerFailed);
        Assert.Equal(3, (int)RadarProcessingArchiveQueuedOverlapStatus.ConsumerFaulted);
        Assert.Equal(4, (int)RadarProcessingArchiveQueuedOverlapStatus.Canceled);
        Assert.Equal(5, (int)RadarProcessingArchiveQueuedOverlapStatus.Disposed);

        Assert.Equal(0, (int)RadarProcessingArchiveQueuedOverlapProducerStatus.NotStarted);
        Assert.Equal(1, (int)RadarProcessingArchiveQueuedOverlapProducerStatus.Completed);
        Assert.Equal(2, (int)RadarProcessingArchiveQueuedOverlapProducerStatus.Failed);
        Assert.Equal(3, (int)RadarProcessingArchiveQueuedOverlapProducerStatus.Canceled);

        Assert.Same(
            RadarProcessingProviderQueueOptions.Default,
            RadarProcessingArchiveQueuedOverlapOptions.Default.QueueOptions);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingArchiveQueuedOverlapResult(
                (RadarProcessingArchiveQueuedOverlapStatus)255,
                RadarProcessingArchiveQueuedOverlapProducerResult.Canceled(),
                RadarProcessingArchiveQueuedOverlapConsumerResult.Canceled()));
        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingArchiveQueuedOverlapProducerResult.Completed(
                null!,
                new RadarProcessingArchiveQueuedProviderResult()));
        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingArchiveQueuedOverlapProducerResult.Failed(null!));
    }

    [Fact]
    public async Task OverlapRunnerLetsProducerQueueAheadWhileConsumerDrainsInOrder()
    {
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var options = new RadarProcessingArchiveQueuedOverlapOptions(
            new RadarProcessingProviderQueueOptions(capacity: 4, recentDetailCapacity: 16));

        var result = await runner.RunAsync(
            (publisher, cancellationToken) =>
            {
                publisher.Publish(CreateOwnedBatch(1), cancellationToken);
                publisher.Publish(CreateOwnedBatch(3), cancellationToken);
                publisher.Publish(CreateOwnedBatch(5), cancellationToken);
                return CreatePublishResult(batchCount: 3);
            },
            async (queue, cancellationToken) =>
            {
                await Task.Delay(100, cancellationToken);
                return await DrainAllAsync(queue, cancellationToken);
            },
            options);

        Assert.True(result.IsCompleted);
        Assert.Equal(RadarProcessingArchiveQueuedOverlapStatus.Completed, result.Status);
        Assert.True(result.Producer.IsCompleted);
        Assert.True(result.Consumer.IsCompleted);
        Assert.Equal(3, result.Producer.PublishResult!.BatchCount);
        Assert.Equal(3, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(0, result.ProviderResult.RejectedPublishCount);
        Assert.Equal(3, result.QueueTelemetry.EnqueuedBatchCount);
        Assert.Equal(3, result.QueueTelemetry.DequeuedBatchCount);
        Assert.Equal(3, result.Consumer.SessionResult.ProcessingResults.Count);
        Assert.Equal([0L, 1L, 2L], result.Consumer.SessionResult.ProcessingResults
            .Select(static item => item.Sequence.Value)
            .ToArray());
        Assert.True(result.QueueTelemetry.QueueDepthHighWatermark > 1);
    }

    [Fact]
    public async Task ConsumerFailureStopsProducerIntakeAndFaultsOverlap()
    {
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        using var consumerFaulted = new ManualResetEventSlim();
        var options = new RadarProcessingArchiveQueuedOverlapOptions(
            new RadarProcessingProviderQueueOptions(
                capacity: 2,
                fullMode: RadarProcessingProviderQueueFullMode.Wait,
                enqueueTimeout: TimeSpan.FromSeconds(5),
                recentDetailCapacity: 16));

        var result = await runner.RunAsync(
            (publisher, cancellationToken) =>
            {
                publisher.Publish(CreateOwnedBatch(1), cancellationToken);
                Assert.True(consumerFaulted.Wait(TimeSpan.FromSeconds(5)));
                publisher.Publish(CreateOwnedBatch(3), cancellationToken);
                return CreatePublishResult(batchCount: 2);
            },
            async (queue, cancellationToken) =>
            {
                var first = await queue.DequeueAsync(cancellationToken);
                Assert.True(first.HasItem);

                const string failure = "processing failed";
                queue.Fault(failure);
                consumerFaulted.Set();
                return CreateSessionResult(
                    queue,
                    [
                        RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                            first.Batch!.Sequence,
                            failure)
                    ],
                    RadarProcessingQueuedSessionStatus.Faulted,
                    failure);
            },
            options);

        Assert.Equal(RadarProcessingArchiveQueuedOverlapStatus.ConsumerFaulted, result.Status);
        Assert.True(result.IsFaulted);
        Assert.True(result.Consumer.IsFaulted);
        Assert.True(result.Producer.IsFailed);
        Assert.Equal("processing failed", result.Message);
        Assert.Equal(2, result.ProviderResult.PublishAttemptCount);
        Assert.Equal(1, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(1, result.ProviderResult.RejectedPublishCount);
        Assert.Equal(1, result.QueueTelemetry.DequeuedBatchCount);
        Assert.Single(result.Consumer.SessionResult.ProcessingResults);
    }

    private static async ValueTask<RadarProcessingQueuedSessionResult> DrainAllAsync(
        RadarProcessingOwnedBatchQueue queue,
        CancellationToken cancellationToken)
    {
        var processingResults = new List<RadarProcessingQueuedBatchProcessingResult>();
        while (true)
        {
            var dequeue = await queue.DequeueAsync(cancellationToken);
            switch (dequeue.Status)
            {
                case RadarProcessingOwnedBatchDequeueStatus.Item:
                    processingResults.Add(
                        RadarProcessingQueuedBatchProcessingResult.Succeeded(
                            dequeue.Batch!.Sequence,
                            CreateProcessingResult()));
                    break;

                case RadarProcessingOwnedBatchDequeueStatus.Closed:
                    return CreateSessionResult(queue, processingResults);

                default:
                    return CreateSessionResult(
                        queue,
                        processingResults,
                        RadarProcessingQueuedSessionStatus.Faulted,
                        dequeue.Message);
            }
        }
    }

    private static RadarProcessingQueuedSessionResult CreateSessionResult(
        RadarProcessingOwnedBatchQueue queue,
        IReadOnlyCollection<RadarProcessingQueuedBatchProcessingResult> processingResults,
        RadarProcessingQueuedSessionStatus status = RadarProcessingQueuedSessionStatus.Completed,
        string message = "")
    {
        var queueSummary = queue.CreateTelemetrySummary();
        var completed = processingResults.LongCount(static result =>
            result.Status == RadarProcessingQueuedBatchProcessingStatus.Succeeded);
        var failed = processingResults.LongCount(static result =>
            result.Status is RadarProcessingQueuedBatchProcessingStatus.FailedProcessing or
                RadarProcessingQueuedBatchProcessingStatus.FailedValidation or
                RadarProcessingQueuedBatchProcessingStatus.FailedMigration);
        var canceled = processingResults.LongCount(static result =>
            result.Status == RadarProcessingQueuedBatchProcessingStatus.Canceled);
        var skipped = processingResults.LongCount(static result =>
            result.Status == RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault);
        var telemetry = new RadarProcessingProviderQueueTelemetrySummary(
            queueSummary.OwnedSnapshotCount,
            queueSummary.OwnedSnapshotPayloadBytes,
            queueSummary.OwnedSnapshotAllocatedBytes,
            queueSummary.TotalOwnedSnapshotTime,
            queueSummary.EnqueueAttemptCount,
            queueSummary.EnqueuedBatchCount,
            queueSummary.EnqueueFullCount,
            queueSummary.EnqueueTimedOutCount,
            queueSummary.EnqueueCanceledCount,
            queueSummary.EnqueueClosedCount,
            queueSummary.EnqueueFaultedCount,
            queueSummary.TotalEnqueueWaitTime,
            queueSummary.DequeuedBatchCount,
            completed,
            failed,
            canceled,
            skipped,
            queueSummary.TotalDrainTime,
            queueSummary.QueueDepthHighWatermark,
            queueSummary.QueuedPayloadBytesHighWatermark,
            queueSummary.OwnedSnapshotPayloadValueCount,
            queueSummary.TotalProviderToProcessingLatency,
            queueSummary.RecentDetails,
            queueSummary.DroppedRecentDetailCount);

        return new RadarProcessingQueuedSessionResult(
            status,
            telemetry,
            processingResults: processingResults,
            message: message);
    }

    private static RadarEventBatch CreateOwnedBatch(
        byte firstPayloadValue)
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: 2);
        builder.AddEvent(
            new RadarStreamIdentity(
                sourceId: 0,
                radarOrdinal: 0,
                momentId: 0,
                elevationSlot: 0,
                azimuthBucket: 0,
                rangeBand: 0,
                dictionaryVersion: DictionaryVersion.Initial,
                sourceUniverseVersion: SourceUniverseVersion.Initial),
            volumeTimestampUtcTicks: 1,
            messageTimestampUtcTicks: 2,
            sourceRecord: 0,
            sourceMessage: 0,
            radialSequence: 0,
            gateStart: 0,
            gateCount: 2,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: [firstPayloadValue, (byte)(firstPayloadValue + 1)]);
        return builder.Build();
    }

    private static RadarProcessingResult CreateProcessingResult()
    {
        var metrics = new RadarProcessingMetrics(
            ProcessedBatchCount: 1,
            ProcessedStreamEventCount: 1,
            ProcessedPayloadValueCount: 2,
            ActiveSourceCount: 1,
            RawValueChecksum: 3,
            ProcessingChecksum: 7);

        return new RadarProcessingResult(
            RadarProcessingExecutionMode.Sequential,
            partitionCount: 1,
            shardCount: 1,
            metrics,
            RadarProcessingValidationResult.Valid(metrics));
    }

    private static ArchiveRadarEventBatchPublishResult CreatePublishResult(
        long batchCount)
    {
        var normalizer = new RadarStreamIdentityNormalizer(
            ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse);
        return new ArchiveRadarEventBatchPublishResult(
            FilePath: "synthetic",
            Decompressor: "synthetic",
            DegreeOfParallelism: 1,
            FileSizeBytes: batchCount * 2,
            CompressedRecordCount: checked((int)batchCount),
            CompressedBytes: batchCount,
            DecompressedBytes: batchCount * 2,
            StreamSchemaVersion: StreamSchemaVersion.Current,
            DictionaryVersion: DictionaryVersion.Initial,
            SourceUniverseVersion: SourceUniverseVersion.Initial,
            BatchCount: batchCount,
            EventCount: batchCount,
            PayloadBytes: batchCount * 2,
            PayloadValueCount: batchCount * 2,
            RawValueChecksum: 0,
            DictionarySnapshot: normalizer.CreateDictionarySnapshot(DictionaryVersion.Initial));
    }

}
