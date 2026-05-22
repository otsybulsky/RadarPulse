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
        Assert.Null(RadarProcessingArchiveQueuedOverlapOptions.Default.RetainedPayloadFactory);

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

        var pressure = new RadarProcessingRetainedResourcePressureSummary(
            pendingRetainedBatchCountHighWatermark: 2,
            pendingRetainedPayloadBytesHighWatermark: 4,
            activeRetainedBatchCountHighWatermark: 1,
            activeRetainedPayloadBytesHighWatermark: 3,
            combinedRetainedBatchCountHighWatermark: 3,
            combinedRetainedPayloadBytesHighWatermark: 7);
        var queueTelemetry = new RadarProcessingProviderQueueTelemetrySummary(
            ownedSnapshotCount: 2,
            ownedSnapshotPayloadBytes: 4,
            ownedSnapshotAllocatedBytes: 128,
            totalOwnedSnapshotTime: TimeSpan.FromMilliseconds(1),
            totalEnqueueWaitTime: TimeSpan.FromMilliseconds(2),
            queueDepthHighWatermark: 2,
            queuedPayloadBytesHighWatermark: 4,
            totalDequeueWaitTime: TimeSpan.FromMilliseconds(3),
            ownedSnapshotPayloadValueCount: 4,
            ownedSnapshotEventCount: 2,
            retainedResourcePressure: pressure);
        var retentionTelemetry = new RadarProcessingRetainedPayloadTelemetrySummary(
            RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
            retentionAttemptCount: 2,
            retainedBatchCount: 2,
            retainedEventCount: 2,
            retainedPayloadBytes: 4,
            retainedPayloadValueCount: 4,
            allocatedBytes: 128,
            totalRetentionTime: TimeSpan.FromMilliseconds(1),
            releaseAttemptCount: 2,
            releaseNotRequiredCount: 2);
        var overlap = new RadarProcessingArchiveOverlapTelemetrySummary(
            RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
            elapsed: TimeSpan.FromMilliseconds(10),
            producerActiveTime: TimeSpan.FromMilliseconds(6),
            consumerActiveTime: TimeSpan.FromMilliseconds(7),
            overlapElapsed: TimeSpan.FromMilliseconds(6),
            measuredAllocatedBytes: 256,
            queueTelemetry,
            retentionTelemetry);

        Assert.True(overlap.HasProducerConsumerOverlap);
        Assert.True(overlap.HasQueuedAheadOverlap);
        Assert.Equal(2, overlap.RetainedEventCount);
        Assert.Equal(4, overlap.RetainedPayloadBytes);
        Assert.Equal(128, overlap.RetentionAllocatedBytes);
        Assert.Same(pressure, overlap.RetainedResourcePressure);
        Assert.Equal(4, overlap.RetainedPayloadBytesHighWatermark);
        Assert.Equal(2, overlap.PendingRetainedBatchCountHighWatermark);
        Assert.Equal(4, overlap.PendingRetainedPayloadBytesHighWatermark);
        Assert.Equal(1, overlap.ActiveRetainedBatchCountHighWatermark);
        Assert.Equal(3, overlap.ActiveRetainedPayloadBytesHighWatermark);
        Assert.Equal(3, overlap.CombinedRetainedBatchCountHighWatermark);
        Assert.Equal(7, overlap.CombinedRetainedPayloadBytesHighWatermark);
        Assert.Equal(TimeSpan.FromMilliseconds(2), overlap.ProviderBlockedTime);
        Assert.Equal(TimeSpan.FromMilliseconds(2), overlap.ProducerBlockedTime);
        Assert.Equal(TimeSpan.FromMilliseconds(3), overlap.ConsumerIdleTime);
        Assert.Equal(128, overlap.UnattributedAllocatedBytes);
        Assert.Equal(2, overlap.ReleaseNotRequiredCount);

        var pressureOnly = new RadarProcessingArchiveOverlapTelemetrySummary(
            retainedResourcePressure: pressure);
        Assert.Same(pressure, pressureOnly.RetainedResourcePressure);

        var completed = new RadarProcessingArchiveQueuedOverlapResult(
            RadarProcessingArchiveQueuedOverlapStatus.Completed,
            RadarProcessingArchiveQueuedOverlapProducerResult.Canceled(),
            RadarProcessingArchiveQueuedOverlapConsumerResult.Canceled());

        Assert.Same(RadarProcessingArchiveOverlapTelemetrySummary.Empty, completed.OverlapTelemetry);
        Assert.Same(completed.OverlapTelemetry, completed.Telemetry);
        Assert.Equal(TimeSpan.Zero, RadarProcessingArchiveOverlapTelemetrySummary.Empty.OverlapElapsed);
        Assert.Equal(0, RadarProcessingArchiveOverlapTelemetrySummary.Empty.RetainedBatchCount);
        Assert.Equal(0, RadarProcessingArchiveOverlapTelemetrySummary.Empty.UnattributedAllocatedBytes);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingArchiveOverlapTelemetrySummary(
                producerActiveTime: TimeSpan.FromMilliseconds(1),
                consumerActiveTime: TimeSpan.FromMilliseconds(1),
                overlapElapsed: TimeSpan.FromMilliseconds(2)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingArchiveOverlapTelemetrySummary(elapsed: TimeSpan.FromTicks(-1)));
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
        Assert.Equal(3, result.QueueTelemetry.OwnedSnapshotEventCount);
        Assert.Equal(3, result.Consumer.SessionResult.ProcessingResults.Count);
        Assert.Equal([0L, 1L, 2L], result.Consumer.SessionResult.ProcessingResults
            .Select(static item => item.Sequence.Value)
            .ToArray());
        Assert.True(result.QueueTelemetry.QueueDepthHighWatermark > 1);

        var overlapTelemetry = result.OverlapTelemetry;
        Assert.Equal(result.QueueTelemetry, overlapTelemetry.QueueTelemetry);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.SnapshotCopy, overlapTelemetry.RetentionStrategy);
        Assert.Equal(result.Elapsed, overlapTelemetry.Elapsed);
        Assert.Equal(result.Producer.Elapsed, overlapTelemetry.ProducerActiveTime);
        Assert.Equal(result.Consumer.Elapsed, overlapTelemetry.ConsumerActiveTime);
        Assert.True(overlapTelemetry.HasProducerConsumerOverlap);
        Assert.True(overlapTelemetry.HasQueuedAheadOverlap);
        Assert.Equal(3, overlapTelemetry.RetainedBatchCount);
        Assert.Equal(3, overlapTelemetry.RetainedEventCount);
        Assert.Equal(6, overlapTelemetry.RetainedPayloadBytes);
        Assert.Equal(6, overlapTelemetry.RetainedPayloadValueCount);
        Assert.Equal(result.QueueTelemetry.OwnedSnapshotAllocatedBytes, overlapTelemetry.RetentionAllocatedBytes);
        Assert.Equal(result.QueueTelemetry.TotalOwnedSnapshotTime, overlapTelemetry.TotalRetentionTime);
        Assert.Equal(result.QueueTelemetry.TotalProviderToProcessingLatency, overlapTelemetry.TotalProviderToProcessingLatency);
        Assert.Equal(result.QueueTelemetry.TotalEnqueueWaitTime, overlapTelemetry.ProviderBlockedTime);
        Assert.Equal(result.QueueTelemetry.TotalDequeueWaitTime, overlapTelemetry.ConsumerIdleTime);
        Assert.Same(result.QueueTelemetry.RetainedResourcePressure, overlapTelemetry.RetainedResourcePressure);
        Assert.Equal(result.QueueTelemetry.PendingRetainedPayloadBytesHighWatermark, overlapTelemetry.PendingRetainedPayloadBytesHighWatermark);
        Assert.Equal(result.QueueTelemetry.ActiveRetainedPayloadBytesHighWatermark, overlapTelemetry.ActiveRetainedPayloadBytesHighWatermark);
        Assert.Equal(result.QueueTelemetry.CombinedRetainedPayloadBytesHighWatermark, overlapTelemetry.CombinedRetainedPayloadBytesHighWatermark);
        Assert.Equal(3, overlapTelemetry.ReleaseAttemptCount);
        Assert.Equal(3, overlapTelemetry.ReleaseNotRequiredCount);
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

    [Fact]
    public async Task ProducerFailureReleasesPendingResourcesAndFaultsOverlap()
    {
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var options = new RadarProcessingArchiveQueuedOverlapOptions(
            new RadarProcessingProviderQueueOptions(capacity: 2, recentDetailCapacity: 16),
            new RadarProcessingRetainedPayloadOptions(RadarProcessingRetainedPayloadStrategy.PooledCopy));

        var result = await runner.RunAsync(
            (publisher, cancellationToken) =>
            {
                PublishLeased(publisher, [1, 2], cancellationToken);
                throw new InvalidOperationException("producer failed");
            },
            async (queue, cancellationToken) =>
            {
                await Task.Delay(10, cancellationToken);
                return CreateSessionResult(
                    queue,
                    Array.Empty<RadarProcessingQueuedBatchProcessingResult>());
            },
            options);

        Assert.Equal(RadarProcessingArchiveQueuedOverlapStatus.ProducerFailed, result.Status);
        Assert.True(result.IsFaulted);
        Assert.True(result.Producer.IsFailed);
        Assert.Equal("producer failed", result.Message);
        Assert.Equal(1, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(1, result.QueueTelemetry.EnqueuedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.DequeuedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentPendingRetainedPayloadBytes);
        Assert.Equal(0, result.QueueTelemetry.CurrentActiveRetainedPayloadBytes);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);
        Assert.Equal(1, result.QueueTelemetry.PendingRetainedBatchCountHighWatermark);
        Assert.Equal(2, result.QueueTelemetry.PendingRetainedPayloadBytesHighWatermark);
        Assert.Equal(1, result.ProviderResult.RetentionTelemetry.ReleaseAttemptCount);
        Assert.Equal(1, result.ProviderResult.RetentionTelemetry.ReleasedBatchCount);
        Assert.Equal(0, result.ProviderResult.RetentionTelemetry.ReleaseFailedCount);
    }

    [Fact]
    public async Task CancellationAfterAcceptedEnqueueReleasesPendingResource()
    {
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var options = new RadarProcessingArchiveQueuedOverlapOptions(
            new RadarProcessingProviderQueueOptions(capacity: 2, recentDetailCapacity: 16),
            new RadarProcessingRetainedPayloadOptions(RadarProcessingRetainedPayloadStrategy.PooledCopy));
        using var cancellation = new CancellationTokenSource();

        var result = await runner.RunAsync(
            (publisher, cancellationToken) =>
            {
                PublishLeased(publisher, [1, 2], cancellationToken);
                cancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return CreatePublishResult(batchCount: 1);
            },
            async (queue, cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                return CreateSessionResult(
                    queue,
                    Array.Empty<RadarProcessingQueuedBatchProcessingResult>());
            },
            options,
            cancellation.Token);

        Assert.Equal(RadarProcessingArchiveQueuedOverlapStatus.Canceled, result.Status);
        Assert.True(result.IsCanceled);
        Assert.True(result.Producer.IsCanceled);
        Assert.True(result.Consumer.IsCanceled);
        Assert.Equal(1, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(1, result.QueueTelemetry.EnqueuedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentPendingRetainedPayloadBytes);
        Assert.Equal(0, result.QueueTelemetry.CurrentActiveRetainedPayloadBytes);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);
        Assert.Equal(1, result.ProviderResult.RetentionTelemetry.ReleaseAttemptCount);
        Assert.Equal(1, result.ProviderResult.RetentionTelemetry.ReleasedBatchCount);
        Assert.Equal(0, result.ProviderResult.RetentionTelemetry.ReleaseFailedCount);
    }

    [Fact]
    public async Task ValidationFailureReleasesActiveResourceAndFaultsWithoutFallback()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var rebalanceSession = CreateRebalanceSession(universe);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var options = new RadarProcessingArchiveQueuedOverlapOptions(
            new RadarProcessingProviderQueueOptions(capacity: 2, recentDetailCapacity: 16),
            new RadarProcessingRetainedPayloadOptions(RadarProcessingRetainedPayloadStrategy.PooledCopy));

        var result = await runner.RunRebalanceAsync(
            (publisher, cancellationToken) =>
            {
                PublishLeased(publisher, [1, 2], cancellationToken, sourceId: 1);
                return CreatePublishResult(batchCount: 1);
            },
            rebalanceSession,
            options);

        Assert.Equal(RadarProcessingArchiveQueuedOverlapStatus.ConsumerFaulted, result.Status);
        Assert.True(result.IsFaulted);
        Assert.True(result.Consumer.IsFaulted);
        Assert.True(result.Producer.IsCompleted);
        Assert.Equal(RadarProcessingQueuedSessionStatus.Faulted, result.Consumer.SessionResult.Status);
        Assert.Equal(result.Consumer.SessionResult.Message, result.Message);
        Assert.Equal(1, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(0, result.ProviderResult.RejectedPublishCount);
        Assert.Equal(1, result.QueueTelemetry.EnqueuedBatchCount);
        Assert.Equal(1, result.QueueTelemetry.DequeuedBatchCount);
        Assert.Equal(1, result.QueueTelemetry.FailedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CompletedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentPendingRetainedPayloadBytes);
        Assert.Equal(0, result.QueueTelemetry.CurrentActiveRetainedPayloadBytes);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);
        Assert.Equal(1, result.QueueTelemetry.ActiveRetainedBatchCountHighWatermark);
        Assert.Equal(2, result.QueueTelemetry.ActiveRetainedPayloadBytesHighWatermark);
        Assert.Equal(1, result.ProviderResult.RetentionTelemetry.ReleaseAttemptCount);
        Assert.Equal(1, result.ProviderResult.RetentionTelemetry.ReleasedBatchCount);
        Assert.Equal(0, result.ProviderResult.RetentionTelemetry.ReleaseFailedCount);

        var processing = Assert.Single(result.Consumer.SessionResult.ProcessingResults);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.FailedValidation, processing.Status);
        Assert.Equal(RadarProcessingValidationError.SourceIdOutsideUniverse, processing.ProcessingResult?.Validation.Error);
    }

    [Fact]
    public async Task RebalanceOverlapCapturesLatestTopologyWhenQueuedBatchWaitsBehindMigration()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var rebalanceSession = CreateRebalanceSession(universe);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var options = new RadarProcessingArchiveQueuedOverlapOptions(
            new RadarProcessingProviderQueueOptions(capacity: 4, recentDetailCapacity: 16));

        var result = await runner.RunAsync(
            (publisher, cancellationToken) =>
            {
                publisher.Publish(
                    CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1]),
                    cancellationToken);
                publisher.Publish(
                    CreateEmptyBatch(universe.Version),
                    cancellationToken);
                return CreatePublishResult(batchCount: 2);
            },
            async (queue, cancellationToken) =>
            {
                await Task.Delay(100, cancellationToken);
                await using var queuedSession = new RadarProcessingQueuedRebalanceSession(
                    rebalanceSession,
                    queue);
                return await queuedSession.DrainAsync(cancellationToken);
            },
            options);

        Assert.True(result.IsCompleted);
        Assert.Equal(2, result.ProviderResult.AcceptedPublishCount);
        Assert.True(result.QueueTelemetry.QueueDepthHighWatermark > 1);
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), result.Consumer.SessionResult.FinalTopologyVersion);

        var first = result.Consumer.SessionResult.ProcessingResults[0];
        var second = result.Consumer.SessionResult.ProcessingResults[1];

        Assert.True(first.RebalanceResult!.PublishedMigration);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, first.TopologyVersion);
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), second.TopologyVersion);
        var secondTelemetry = Assert.IsType<RadarProcessingTelemetry>(second.ProcessingResult!.Telemetry);
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), secondTelemetry.TopologyVersion);
        Assert.Equal(1, secondTelemetry.Partitions[0].ShardId);
    }

    [Fact]
    public async Task RunRebalanceAsyncUsesOrderedConsumerAndReportsFinalTopology()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var rebalanceSession = CreateRebalanceSession(universe);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var options = new RadarProcessingArchiveQueuedOverlapOptions(
            new RadarProcessingProviderQueueOptions(capacity: 2, recentDetailCapacity: 16));

        var result = await runner.RunRebalanceAsync(
            (publisher, cancellationToken) =>
            {
                publisher.Publish(
                    CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1]),
                    cancellationToken);
                publisher.Publish(
                    CreateEmptyBatch(universe.Version),
                    cancellationToken);
                return CreatePublishResult(batchCount: 2);
            },
            rebalanceSession,
            options);

        Assert.True(result.IsCompleted);
        Assert.True(result.Consumer.SessionResult.IsCompleted);
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), result.Consumer.SessionResult.FinalTopologyVersion);
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), rebalanceSession.CurrentTopology.Version);
        Assert.Equal(
            [RadarProcessingTopologyVersion.Initial, RadarProcessingTopologyVersion.Initial.Next()],
            result.Consumer.SessionResult.ProcessingResults
                .Select(static processing => processing.TopologyVersion)
                .ToArray());
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);
        Assert.InRange(result.QueueTelemetry.PendingRetainedBatchCountHighWatermark, 1, 2);
        Assert.Equal(6, result.QueueTelemetry.PendingRetainedPayloadBytesHighWatermark);
        Assert.Equal(1, result.QueueTelemetry.ActiveRetainedBatchCountHighWatermark);
        Assert.Equal(6, result.QueueTelemetry.ActiveRetainedPayloadBytesHighWatermark);
        Assert.True(result.QueueTelemetry.CombinedRetainedBatchCountHighWatermark >= result.QueueTelemetry.ActiveRetainedBatchCountHighWatermark);
        Assert.True(result.QueueTelemetry.CombinedRetainedBatchCountHighWatermark >= result.QueueTelemetry.PendingRetainedBatchCountHighWatermark);
        Assert.Equal(6, result.QueueTelemetry.CombinedRetainedPayloadBytesHighWatermark);
        Assert.Same(result.QueueTelemetry.RetainedResourcePressure, result.RetainedResourcePressure);
        Assert.Same(result.QueueTelemetry.RetainedResourcePressure, result.OverlapTelemetry.RetainedResourcePressure);
        Assert.Equal(result.QueueTelemetry.PendingRetainedPayloadBytesHighWatermark, result.OverlapTelemetry.PendingRetainedPayloadBytesHighWatermark);
        Assert.Equal(result.QueueTelemetry.ActiveRetainedPayloadBytesHighWatermark, result.OverlapTelemetry.ActiveRetainedPayloadBytesHighWatermark);
        Assert.Equal(result.QueueTelemetry.CombinedRetainedPayloadBytesHighWatermark, result.OverlapTelemetry.CombinedRetainedPayloadBytesHighWatermark);
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
            queueSummary.DroppedRecentDetailCount,
            queueSummary.OwnedSnapshotEventCount,
            queueSummary.TotalDequeueWaitTime);

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

    private static RadarProcessingRebalanceSession CreateRebalanceSession(
        RadarSourceUniverse universe)
    {
        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount: universe.SourceCount,
                shardCount: Math.Min(2, universe.SourceCount)));

        return new RadarProcessingRebalanceSession(
            core,
            new RadarProcessingPressureOptions(
                eventWeight: 1.0,
                payloadValueWeight: 0.0,
                rawValueChecksumWeight: 0.0),
            new RadarProcessingPressureWindow(
                new RadarProcessingPressureWindowOptions(
                    sampleCapacity: 2,
                    minimumSampleCount: 1,
                    coldThreshold: 0.0,
                    warmExitThreshold: 4.0,
                    warmEnterThreshold: 4.5,
                    hotExitThreshold: 4.75,
                    hotEnterThreshold: 5.0,
                    superHotExitThreshold: 9.0,
                    superHotEnterThreshold: 10.0)),
            new RadarProcessingRebalancePolicyState(
                universe.SourceCount,
                shardCount: Math.Min(2, universe.SourceCount),
                new RadarProcessingRebalanceOptions(
                    budgetWindowEvaluationCount: 4,
                    globalMoveBudgetPerWindow: 4,
                    sourceShardMoveBudgetPerWindow: 4,
                    targetShardReceiveBudgetPerWindow: 4,
                    minimumPartitionResidencyEvaluations: 0,
                    partitionMoveCooldownEvaluations: 0,
                    sourceShardMoveCooldownEvaluations: 0,
                    targetShardReceiveCooldownEvaluations: 0,
                    minimumProjectedBenefit: 0.05)),
            telemetryRecorder: new RadarProcessingRebalanceTelemetryRecorder(
                new RadarProcessingTelemetryRetentionOptions(
                    RadarProcessingDiagnosticRetentionMode.Recent,
                    maxRetainedDecisions: 8,
                    maxRetainedLifecycleTransitions: 8,
                    maxRetainedAcceptedMoves: 8,
                    maxRetainedValidationFailures: 8)));
    }

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

    private static void PublishLeased(
        IArchiveRadarEventBatchPublisher publisher,
        byte[] payload,
        CancellationToken cancellationToken,
        int sourceId = 0)
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: payload.Length);
        builder.AddEvent(
            new RadarStreamIdentity(
                sourceId,
                radarOrdinal: 0,
                momentId: 0,
                elevationSlot: 0,
                azimuthBucket: 0,
                rangeBand: 0,
                dictionaryVersion: DictionaryVersion.Initial,
                sourceUniverseVersion: SourceUniverseVersion.Initial),
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks: 100,
            sourceRecord: 1,
            sourceMessage: 1,
            radialSequence: 1,
            gateStart: 0,
            gateCount: (ushort)payload.Length,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: payload);

        builder.ConsumeLeased(batch => publisher.Publish(batch, cancellationToken));
    }
}
