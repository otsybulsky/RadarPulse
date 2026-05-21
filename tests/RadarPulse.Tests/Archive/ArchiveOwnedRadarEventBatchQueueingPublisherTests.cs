using System.Buffers;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Archive;

public sealed class ArchiveOwnedRadarEventBatchQueueingPublisherTests
{
    [Fact]
    public async Task PublishCopiesLeasedBatchToOwnedQueueBeforeCallbackReturns()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 2));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(queue);
        var builder = CreateBuilder([1, 2]);
        var callbackObservedEnqueuedOwnedBatch = false;

        builder.ConsumeLeased(batch =>
        {
            Assert.Equal(RadarEventBatchLifetime.Leased, batch.Lifetime);
            publisher.Publish(batch, CancellationToken.None);
            callbackObservedEnqueuedOwnedBatch = queue.PendingCount == 1;
        });
        AddEvent(builder, [9, 10]);

        var dequeue = await queue.DequeueAsync();
        var queued = dequeue.Batch!;
        Assert.True(callbackObservedEnqueuedOwnedBatch);
        Assert.Equal(RadarProcessingOwnedBatchDequeueStatus.Item, dequeue.Status);
        Assert.Equal(RadarEventBatchLifetime.Owned, queued.Batch.Lifetime);
        Assert.Equal([1, 2], queued.Batch.Payload.ToArray());
        Assert.True(queued.Batch.TryGetPayloadMetrics(out var payloadValueCount, out var rawValueChecksum));
        Assert.Equal(2, payloadValueCount);
        Assert.Equal(3, rawValueChecksum);
        Assert.Single(publisher.EnqueueResults);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Accepted, publisher.EnqueueResults[0].Status);
        Assert.Equal(1, publisher.CreateResult().Telemetry.OwnedSnapshotCount);
        Assert.True(publisher.CreateResult().Telemetry.TotalOwnedSnapshotTime >= TimeSpan.Zero);
    }

    [Fact]
    public void PublishRecordsBackpressureResultAndThrowsWhenQueueReturnsFull()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(
                capacity: 1,
                fullMode: RadarProcessingProviderQueueFullMode.ReturnFull));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(queue);

        PublishLeased(publisher, [1]);
        var exception = Assert.Throws<InvalidOperationException>(() =>
            PublishLeased(publisher, [2]));
        var result = publisher.CreateResult();

        Assert.Contains("Full", exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, result.PublishAttemptCount);
        Assert.Equal(1, result.AcceptedPublishCount);
        Assert.Equal(1, result.RejectedPublishCount);
        Assert.True(result.HasRejectedPublish);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Full, result.LastEnqueueResult?.Status);
        Assert.Equal(1, result.Telemetry.EnqueuedBatchCount);
        Assert.Equal(1, result.Telemetry.EnqueueFullCount);
        Assert.Equal(1, queue.PendingCount);
    }

    [Fact]
    public void ProcessingFaultCausesLaterPublishToFailWithoutEnqueueingPartialWork()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 2));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(queue);
        PublishLeased(publisher, [1]);

        queue.Fault("processing failed");
        var exception = Assert.Throws<InvalidOperationException>(() =>
            PublishLeased(publisher, [2]));
        var result = publisher.CreateResult();

        Assert.Contains("Faulted", exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, result.PublishAttemptCount);
        Assert.Equal(1, result.AcceptedPublishCount);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Faulted, result.LastEnqueueResult?.Status);
        Assert.Equal(1, queue.PendingCount);
    }

    [Fact]
    public void CancellationDuringPublishDoesNotEnqueuePartialWork()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 2));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(queue);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            PublishLeased(publisher, [1], cancellation.Token));
        var result = publisher.CreateResult();

        Assert.Equal(0, queue.PendingCount);
        Assert.Equal(1, result.PublishAttemptCount);
        Assert.Equal(0, result.AcceptedPublishCount);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Canceled, result.LastEnqueueResult?.Status);
    }

    [Fact]
    public async Task ConsumerResourceLeaseMovesPressureFromPendingToActiveUntilRelease()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 2));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(
            queue,
            retainedPayloadOptions: new RadarProcessingRetainedPayloadOptions(
                RadarProcessingRetainedPayloadStrategy.PooledCopy));

        PublishLeased(publisher, [1, 2]);
        PublishLeased(publisher, [3, 4, 5]);

        var dequeue = await queue.DequeueAsync();
        Assert.Equal(RadarProcessingOwnedBatchDequeueStatus.Item, dequeue.Status);

        using var lease = publisher.AcquireConsumerResourceLease(dequeue.Batch!.Sequence);
        var activeTelemetry = publisher.CreateResult().Telemetry;

        Assert.Equal(1, activeTelemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(3, activeTelemetry.CurrentPendingRetainedPayloadBytes);
        Assert.Equal(2, activeTelemetry.PendingRetainedBatchCountHighWatermark);
        Assert.Equal(5, activeTelemetry.PendingRetainedPayloadBytesHighWatermark);
        Assert.Equal(1, activeTelemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(2, activeTelemetry.CurrentActiveRetainedPayloadBytes);
        Assert.Equal(1, activeTelemetry.ActiveRetainedBatchCountHighWatermark);
        Assert.Equal(2, activeTelemetry.ActiveRetainedPayloadBytesHighWatermark);
        Assert.Equal(2, activeTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(5, activeTelemetry.CurrentCombinedRetainedPayloadBytes);
        Assert.Equal(2, activeTelemetry.CombinedRetainedBatchCountHighWatermark);
        Assert.Equal(5, activeTelemetry.CombinedRetainedPayloadBytesHighWatermark);

        var release = lease.Release();
        var releasedTelemetry = publisher.CreateResult().Telemetry;

        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.Released, release.Status);
        Assert.Equal(1, releasedTelemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(3, releasedTelemetry.CurrentPendingRetainedPayloadBytes);
        Assert.Equal(0, releasedTelemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, releasedTelemetry.CurrentActiveRetainedPayloadBytes);
        Assert.Equal(1, releasedTelemetry.ActiveRetainedBatchCountHighWatermark);

        var cleanup = publisher.ReleasePendingResources();
        var cleanedTelemetry = publisher.CreateResult().Telemetry;

        Assert.Single(cleanup.ReleaseResults);
        Assert.Equal(0, cleanedTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, cleanedTelemetry.CurrentCombinedRetainedPayloadBytes);
        Assert.Equal(2, publisher.CreateResult().RetentionTelemetry.ReleaseAttemptCount);
        Assert.Equal(2, publisher.CreateResult().RetentionTelemetry.ReleasedBatchCount);
    }

    [Fact]
    public async Task WaitingConsumerCanAcquireRetainedResourceForAcceptedPublish()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 1));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(
            queue,
            retainedPayloadOptions: new RadarProcessingRetainedPayloadOptions(
                RadarProcessingRetainedPayloadStrategy.PooledCopy));

        var consumer = Task.Run(async () =>
        {
            var dequeue = await queue.DequeueAsync();
            Assert.Equal(RadarProcessingOwnedBatchDequeueStatus.Item, dequeue.Status);

            using var lease = publisher.AcquireConsumerResourceLease(dequeue.Batch!.Sequence);
            return publisher.CreateResult().Telemetry;
        });

        await Task.Delay(50);
        PublishLeased(publisher, [1, 2, 3]);
        var telemetry = await consumer.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(0, telemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(1, telemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(3, telemetry.CurrentActiveRetainedPayloadBytes);
        Assert.Equal(1, telemetry.ActiveRetainedBatchCountHighWatermark);
    }

    [Fact]
    public async Task ConsumerResourcePressureUsesBatchPayloadBytesWhenReleaseIsNotRequired()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 1));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(queue);

        PublishLeased(publisher, [1, 2, 3]);

        var dequeue = await queue.DequeueAsync();
        using var lease = publisher.AcquireConsumerResourceLease(dequeue.Batch!.Sequence);
        var activeTelemetry = publisher.CreateResult().Telemetry;

        Assert.Equal(1, activeTelemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(3, activeTelemetry.CurrentActiveRetainedPayloadBytes);
        Assert.Equal(3, activeTelemetry.ActiveRetainedPayloadBytesHighWatermark);

        var release = lease.Release();
        var released = publisher.CreateResult();

        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.NotRequired, release.Status);
        Assert.Equal(0, released.Telemetry.CurrentCombinedRetainedPayloadBytes);
        Assert.Equal(1, released.RetentionTelemetry.ReleaseNotRequiredCount);
    }

    [Fact]
    public void ReleasePendingResourcesRecordsReleaseFailureAndClearsPendingPressure()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 1));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(
            queue,
            retainedPayloadOptions: new RadarProcessingRetainedPayloadOptions(
                RadarProcessingRetainedPayloadStrategy.PooledCopy),
            retainedPayloadFactory: new RadarProcessingRetainedPayloadFactory(
                ArrayPool<RadarStreamEvent>.Shared,
                new ThrowingReturnArrayPool<byte>()));

        PublishLeased(publisher, [1, 2, 3]);
        var pending = publisher.CreateResult();

        Assert.Equal(1, pending.Telemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(3, pending.Telemetry.CurrentPendingRetainedPayloadBytes);

        var cleanup = publisher.ReleasePendingResources();
        var result = publisher.CreateResult();
        var readiness = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateRetainedResourceReleaseHealth(
            result.RetentionTelemetry);

        Assert.False(cleanup.IsSuccessful);
        Assert.Equal(1, cleanup.FailedCount);
        Assert.Equal(0, result.Telemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, result.Telemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, result.Telemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(1, result.RetentionTelemetry.ReleaseAttemptCount);
        Assert.Equal(1, result.RetentionTelemetry.ReleaseFailedCount);
        Assert.True(readiness.IsFailed);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.RetainedResourceReleaseFailed, readiness.Error);
    }

    [Fact]
    public async Task ConsumerReleaseFailureRecordsReadinessFailureAndClearsActivePressure()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 1));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(
            queue,
            retainedPayloadOptions: new RadarProcessingRetainedPayloadOptions(
                RadarProcessingRetainedPayloadStrategy.PooledCopy),
            retainedPayloadFactory: new RadarProcessingRetainedPayloadFactory(
                ArrayPool<RadarStreamEvent>.Shared,
                new ThrowingReturnArrayPool<byte>()));

        PublishLeased(publisher, [1, 2, 3]);
        var dequeue = await queue.DequeueAsync();

        using var lease = publisher.AcquireConsumerResourceLease(dequeue.Batch!.Sequence);
        var active = publisher.CreateResult();

        Assert.Equal(0, active.Telemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(1, active.Telemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(3, active.Telemetry.CurrentActiveRetainedPayloadBytes);

        var release = lease.Release();
        var result = publisher.CreateResult();
        var readiness = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateRetainedResourceReleaseHealth(
            result.RetentionTelemetry);

        Assert.True(release.IsFailure);
        Assert.Equal(0, result.Telemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, result.Telemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, result.Telemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(1, result.RetentionTelemetry.ReleaseAttemptCount);
        Assert.Equal(0, result.RetentionTelemetry.ReleasedBatchCount);
        Assert.Equal(1, result.RetentionTelemetry.ReleaseFailedCount);
        Assert.True(readiness.IsFailed);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.RetainedResourceReleaseFailed, readiness.Error);
    }

    [Fact]
    public void RetentionFailureStopsCurrentPublishAndLeavesAcceptedResourcesForTerminalCleanup()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 2));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(
            queue,
            retainedPayloadOptions: new RadarProcessingRetainedPayloadOptions(
                RadarProcessingRetainedPayloadStrategy.PooledCopy),
            retainedPayloadFactory: new RadarProcessingRetainedPayloadFactory(
                ArrayPool<RadarStreamEvent>.Shared,
                new FailingRentArrayPool<byte>(successfulRentCount: 1)));

        PublishLeased(publisher, [1, 2]);
        var exception = Assert.Throws<InvalidOperationException>(() =>
            PublishLeased(publisher, [3, 4]));
        var rejected = publisher.CreateResult();

        Assert.Contains("FailedCopy", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, rejected.PublishAttemptCount);
        Assert.Equal(1, rejected.AcceptedPublishCount);
        Assert.Equal(2, rejected.RetentionTelemetry.RetentionAttemptCount);
        Assert.Equal(1, rejected.RetentionTelemetry.RetentionFailedCopyCount);
        Assert.Equal(1, rejected.Telemetry.CurrentPendingRetainedBatchCount);

        var cleanup = publisher.ReleasePendingResources();
        var cleaned = publisher.CreateResult();

        Assert.True(cleanup.IsSuccessful);
        Assert.Equal(1, cleanup.ReleasedCount);
        Assert.Equal(0, cleaned.Telemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(1, cleaned.RetentionTelemetry.ReleaseAttemptCount);
        Assert.Equal(1, cleaned.RetentionTelemetry.ReleasedBatchCount);
    }

    private static void PublishLeased(
        ArchiveOwnedRadarEventBatchQueueingPublisher publisher,
        byte[] payload,
        CancellationToken cancellationToken = default)
    {
        var builder = CreateBuilder(payload);
        builder.ConsumeLeased(batch => publisher.Publish(batch, cancellationToken));
    }

    private static RadarEventBatchBuilder CreateBuilder(byte[] payload)
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: payload.Length);
        AddEvent(builder, payload);
        return builder;
    }

    private static void AddEvent(
        RadarEventBatchBuilder builder,
        byte[] payload)
    {
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
    }

    private sealed class ThrowingReturnArrayPool<T> : ArrayPool<T>
    {
        public override T[] Rent(int minimumLength) => new T[minimumLength];

        public override void Return(T[] array, bool clearArray = false) =>
            throw new InvalidOperationException("pool return failed");
    }

    private sealed class FailingRentArrayPool<T> : ArrayPool<T>
    {
        private readonly int successfulRentCount;
        private int rentCount;

        public FailingRentArrayPool(int successfulRentCount)
        {
            this.successfulRentCount = successfulRentCount;
        }

        public override T[] Rent(int minimumLength)
        {
            if (rentCount++ >= successfulRentCount)
            {
                throw new InvalidOperationException("pool rent failed");
            }

            return new T[minimumLength];
        }

        public override void Return(T[] array, bool clearArray = false)
        {
        }
    }
}
