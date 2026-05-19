using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingRetainedBatchResourceTests
{
    [Fact]
    public void RetainedBatchResourceStateValuesAreStable()
    {
        Assert.Equal(1, (int)RadarProcessingRetainedBatchResourceState.ProviderOwned);
        Assert.Equal(2, (int)RadarProcessingRetainedBatchResourceState.QueueOwned);
        Assert.Equal(3, (int)RadarProcessingRetainedBatchResourceState.ConsumerOwned);
        Assert.Equal(4, (int)RadarProcessingRetainedBatchResourceState.Released);
        Assert.Equal(5, (int)RadarProcessingRetainedBatchResourceState.ReleaseFailed);
    }

    [Fact]
    public void RetainedQueuedBatchTransfersProviderResourceToQueueOwnership()
    {
        var queued = CreateQueuedBatch();
        var resource = new RadarProcessingRetainedBatchResource(
            RadarProcessingRetainedPayloadStrategy.PooledCopy,
            payloadBytes: queued.PayloadBytes);

        var retained = new RadarProcessingRetainedQueuedBatch(queued, resource);

        Assert.Same(queued, retained.QueuedBatch);
        Assert.Same(resource, retained.Resource);
        Assert.Equal(queued.Sequence, retained.Sequence);
        Assert.Equal(RadarProcessingRetainedBatchResourceState.QueueOwned, resource.State);
        Assert.False(retained.HasTerminalResource);

        Assert.Throws<InvalidOperationException>(resource.TransferToQueue);
        Assert.Throws<ArgumentNullException>(() => new RadarProcessingRetainedQueuedBatch(null!));
    }

    [Fact]
    public void ConsumerLeaseTransfersResourceAndReleasesExactlyOnce()
    {
        var releaseCount = 0;
        var queued = CreateQueuedBatch();
        var resource = new RadarProcessingRetainedBatchResource(
            RadarProcessingRetainedPayloadStrategy.PooledCopy,
            payloadBytes: queued.PayloadBytes,
            release: () =>
            {
                releaseCount++;
                return RadarProcessingRetainedPayloadReleaseResult.Released(
                    RadarProcessingRetainedPayloadStrategy.PooledCopy,
                    TimeSpan.FromMilliseconds(1),
                    payloadBytes: queued.PayloadBytes);
            });
        var retained = new RadarProcessingRetainedQueuedBatch(queued, resource);

        using var lease = retained.AcquireForConsumer();

        Assert.Same(retained, lease.RetainedBatch);
        Assert.Same(queued, lease.QueuedBatch);
        Assert.Same(queued.Batch, lease.Batch);
        Assert.Equal(RadarProcessingRetainedBatchResourceState.ConsumerOwned, resource.State);

        var released = lease.Release();
        var second = resource.Release();

        Assert.True(lease.IsDisposed);
        Assert.True(released.IsReleased);
        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.Released, released.Status);
        Assert.Equal(queued.PayloadBytes, released.PayloadBytes);
        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.AlreadyReleased, second.Status);
        Assert.Equal(1, releaseCount);
        Assert.Equal(RadarProcessingRetainedBatchResourceState.Released, resource.State);
        Assert.True(resource.IsTerminal);
        Assert.Same(released, resource.LastReleaseResult);
    }

    [Fact]
    public void PendingCleanupCanReleaseQueueOwnedResources()
    {
        var firstReleased = 0;
        var secondReleased = 0;
        var first = new RadarProcessingRetainedQueuedBatch(
            CreateQueuedBatch(0),
            new RadarProcessingRetainedBatchResource(
                RadarProcessingRetainedPayloadStrategy.PooledCopy,
                payloadBytes: 2,
                release: () =>
                {
                    firstReleased++;
                    return RadarProcessingRetainedPayloadReleaseResult.Released(
                        RadarProcessingRetainedPayloadStrategy.PooledCopy,
                        payloadBytes: 2);
                }));
        var second = new RadarProcessingRetainedQueuedBatch(
            CreateQueuedBatch(1),
            new RadarProcessingRetainedBatchResource(
                RadarProcessingRetainedPayloadStrategy.PooledCopy,
                payloadBytes: 2,
                release: () =>
                {
                    secondReleased++;
                    return RadarProcessingRetainedPayloadReleaseResult.Released(
                        RadarProcessingRetainedPayloadStrategy.PooledCopy,
                        payloadBytes: 2);
                }));

        var cleanup = RadarProcessingRetainedResourceCleanupResult.ReleaseAll(
            [first.Resource, second.Resource]);

        Assert.True(cleanup.IsSuccessful);
        Assert.Equal(2, cleanup.ReleaseAttemptCount);
        Assert.Equal(2, cleanup.ReleasedCount);
        Assert.Equal(0, cleanup.FailedCount);
        Assert.Equal(1, firstReleased);
        Assert.Equal(1, secondReleased);
        Assert.Equal(RadarProcessingRetainedBatchResourceState.Released, first.Resource.State);
        Assert.Equal(RadarProcessingRetainedBatchResourceState.Released, second.Resource.State);
    }

    [Fact]
    public void ReleaseFailureIsTerminalAndDoesNotInvokeCallbackAgain()
    {
        var releaseCount = 0;
        var resource = new RadarProcessingRetainedBatchResource(
            RadarProcessingRetainedPayloadStrategy.PooledCopy,
            payloadBytes: 2,
            release: () =>
            {
                releaseCount++;
                return RadarProcessingRetainedPayloadReleaseResult.Failed(
                    RadarProcessingRetainedPayloadStrategy.PooledCopy,
                    "pool return failed");
            });
        var retained = new RadarProcessingRetainedQueuedBatch(CreateQueuedBatch(), resource);

        var failed = retained.ReleasePending();
        var second = retained.ReleasePending();
        var cleanup = new RadarProcessingRetainedResourceCleanupResult([failed, second]);

        Assert.True(failed.IsFailure);
        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.Failed, failed.Status);
        Assert.Equal("pool return failed", failed.Message);
        Assert.True(second.IsFailure);
        Assert.Equal(1, releaseCount);
        Assert.Equal(RadarProcessingRetainedBatchResourceState.ReleaseFailed, resource.State);
        Assert.True(resource.IsTerminal);
        Assert.False(cleanup.IsSuccessful);
        Assert.Equal(2, cleanup.FailedCount);
    }

    [Fact]
    public void NotRequiredResourceUsesNotRequiredReleaseStatus()
    {
        var retained = new RadarProcessingRetainedQueuedBatch(CreateQueuedBatch());

        var release = retained.ReleasePending();

        Assert.Equal(RadarProcessingRetainedPayloadStrategy.SnapshotCopy, retained.Resource.Strategy);
        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.NotRequired, release.Status);
        Assert.Equal(RadarProcessingRetainedBatchResourceState.Released, retained.Resource.State);
        Assert.True(retained.HasTerminalResource);
    }

    [Fact]
    public void ResourceRejectsInvalidConstructionAndTransitions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedBatchResource(
                (RadarProcessingRetainedPayloadStrategy)255,
                payloadBytes: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedBatchResource(
                RadarProcessingRetainedPayloadStrategy.PooledCopy,
                payloadBytes: -1));

        var resource = new RadarProcessingRetainedBatchResource(
            RadarProcessingRetainedPayloadStrategy.PooledCopy,
            payloadBytes: 2);

        Assert.Throws<InvalidOperationException>(resource.TransferToConsumer);
        resource.TransferToQueue();
        resource.TransferToConsumer();
        Assert.Throws<InvalidOperationException>(resource.TransferToConsumer);
        resource.Release();
        Assert.Throws<InvalidOperationException>(resource.TransferToQueue);
    }

    [Fact]
    public void CleanupResultCopiesReleaseResultsAndRejectsNulls()
    {
        var releases = new List<RadarProcessingRetainedPayloadReleaseResult>
        {
            RadarProcessingRetainedPayloadReleaseResult.Released(
                RadarProcessingRetainedPayloadStrategy.PooledCopy),
            RadarProcessingRetainedPayloadReleaseResult.AlreadyReleased(
                RadarProcessingRetainedPayloadStrategy.PooledCopy),
            RadarProcessingRetainedPayloadReleaseResult.NotRequired(
                RadarProcessingRetainedPayloadStrategy.SnapshotCopy)
        };

        var cleanup = new RadarProcessingRetainedResourceCleanupResult(releases);
        releases.Clear();

        Assert.True(cleanup.IsSuccessful);
        Assert.Equal(3, cleanup.ReleaseAttemptCount);
        Assert.Equal(1, cleanup.ReleasedCount);
        Assert.Equal(1, cleanup.AlreadyReleasedCount);
        Assert.Equal(1, cleanup.NotRequiredCount);
        Assert.Equal(0, cleanup.FailedCount);
        Assert.Equal(3, cleanup.ReleaseResults.Count);
        Assert.Empty(RadarProcessingRetainedResourceCleanupResult.Empty.ReleaseResults);

        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingRetainedResourceCleanupResult(
                new RadarProcessingRetainedPayloadReleaseResult[] { null! }));
        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingRetainedResourceCleanupResult.ReleaseAll(null!));
        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingRetainedResourceCleanupResult.ReleaseAll(
                new RadarProcessingRetainedBatchResource[] { null! }));
    }

    private static RadarProcessingQueuedBatch CreateQueuedBatch(
        long sequence = 0) =>
        new(
            new RadarProcessingQueuedBatchSequence(sequence),
            CreateOwnedBatch());

    private static RadarEventBatch CreateOwnedBatch()
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
            payload: [7, 8]);

        return builder.Build();
    }
}
