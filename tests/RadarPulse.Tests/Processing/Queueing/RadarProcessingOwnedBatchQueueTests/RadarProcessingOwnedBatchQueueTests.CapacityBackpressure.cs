using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingOwnedBatchQueueTests
{
    [Fact]
    public async Task ReturnFullModeRejectsFullQueueAndReusesSpaceAfterDequeue()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(
                capacity: 1,
                fullMode: RadarProcessingProviderQueueFullMode.ReturnFull));

        var first = await queue.EnqueueAsync(
            CreateOwnedBatch(1),
            ownedSnapshotTime: TimeSpan.FromMilliseconds(2),
            ownedSnapshotAllocatedBytes: 64);
        var second = await queue.EnqueueAsync(CreateOwnedBatch(3));

        Assert.True(first.IsAccepted);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Full, second.Status);
        Assert.False(second.IsAccepted);
        Assert.Equal(1, queue.PendingCount);

        var dequeued = await queue.DequeueAsync();
        var third = await queue.EnqueueAsync(CreateOwnedBatch(5));
        var summary = queue.CreateTelemetrySummary();

        Assert.True(dequeued.HasItem);
        Assert.True(third.IsAccepted);
        Assert.Equal(1, third.Sequence?.Value);
        Assert.Equal(1, queue.PendingCount);
        Assert.Equal(3, summary.EnqueueAttemptCount);
        Assert.Equal(2, summary.EnqueuedBatchCount);
        Assert.Equal(1, summary.EnqueueFullCount);
        Assert.Equal(2, summary.OwnedSnapshotCount);
        Assert.Equal(4, summary.OwnedSnapshotPayloadBytes);
        Assert.Equal(4, summary.OwnedSnapshotPayloadValueCount);
        Assert.Equal(64, summary.OwnedSnapshotAllocatedBytes);
        Assert.Equal(TimeSpan.FromMilliseconds(2), summary.TotalOwnedSnapshotTime);
        Assert.Equal(1, summary.DequeuedBatchCount);
        Assert.True(summary.TotalDequeueWaitTime >= TimeSpan.Zero);
        Assert.True(summary.TotalProviderToProcessingLatency >= TimeSpan.Zero);
        Assert.Equal(1, summary.QueueDepthHighWatermark);
        Assert.Equal(2, summary.QueuedPayloadBytesHighWatermark);
        Assert.Equal(1, summary.CurrentPendingRetainedBatchCount);
        Assert.Equal(2, summary.CurrentPendingRetainedPayloadBytes);
        Assert.Equal(1, summary.PendingRetainedBatchCountHighWatermark);
        Assert.Equal(2, summary.PendingRetainedPayloadBytesHighWatermark);
        Assert.Equal(0, summary.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, summary.CurrentActiveRetainedPayloadBytes);
        Assert.Equal(1, summary.CurrentCombinedRetainedBatchCount);
        Assert.Equal(2, summary.CurrentCombinedRetainedPayloadBytes);
        Assert.Equal(1, summary.CombinedRetainedBatchCountHighWatermark);
        Assert.Equal(2, summary.CombinedRetainedPayloadBytesHighWatermark);
        Assert.Equal(4, summary.RecentDetails.Count);
        Assert.Equal(0, summary.DroppedRecentDetailCount);
        Assert.Equal(
            [
                RadarProcessingProviderQueueRecentDetailKind.Enqueue,
                RadarProcessingProviderQueueRecentDetailKind.Enqueue,
                RadarProcessingProviderQueueRecentDetailKind.Dequeue,
                RadarProcessingProviderQueueRecentDetailKind.Enqueue
            ],
            summary.RecentDetails.Select(static detail => detail.Kind).ToArray());
        Assert.True(summary.HasBackpressure);
    }

    [Fact]
    public async Task WaitModeAcceptsBlockedEnqueueAfterDequeueCreatesCapacity()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(
                capacity: 1,
                fullMode: RadarProcessingProviderQueueFullMode.Wait,
                enqueueTimeout: TimeSpan.FromSeconds(5)));

        var first = await queue.EnqueueAsync(CreateOwnedBatch(1));
        var pending = queue.EnqueueAsync(CreateOwnedBatch(3)).AsTask();

        await Task.Delay(50);
        Assert.False(pending.IsCompleted);

        var dequeued = await queue.DequeueAsync();
        var second = await pending;

        Assert.True(first.IsAccepted);
        Assert.True(dequeued.HasItem);
        Assert.True(second.IsAccepted);
        Assert.Equal(1, second.Sequence?.Value);
        Assert.Equal(1, queue.PendingCount);
        Assert.True(second.EnqueueWaitTime > TimeSpan.Zero);
        Assert.True(queue.CreateTelemetrySummary().TotalEnqueueWaitTime > TimeSpan.Zero);
    }

    [Fact]
    public async Task WaitModeCanTimeoutWhileQueueIsFull()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(
                capacity: 1,
                fullMode: RadarProcessingProviderQueueFullMode.Wait,
                enqueueTimeout: TimeSpan.FromMilliseconds(20)));

        var first = await queue.EnqueueAsync(CreateOwnedBatch(1));
        var second = await queue.EnqueueAsync(CreateOwnedBatch(3));

        Assert.True(first.IsAccepted);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.TimedOut, second.Status);
        Assert.False(second.IsAccepted);
        Assert.Equal(1, queue.PendingCount);

        var summary = queue.CreateTelemetrySummary();

        Assert.Equal(2, summary.EnqueueAttemptCount);
        Assert.Equal(1, summary.EnqueuedBatchCount);
        Assert.Equal(1, summary.EnqueueTimedOutCount);
        Assert.True(summary.HasBackpressure);
    }

    [Fact]
    public async Task EnqueueCancellationIsReportedBeforeAndWhileWaiting()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 1));
        using var before = new CancellationTokenSource();
        await before.CancelAsync();

        var canceledBefore = await queue.EnqueueAsync(CreateOwnedBatch(1), cancellationToken: before.Token);

        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Canceled, canceledBefore.Status);
        Assert.Equal(0, queue.PendingCount);

        Assert.True((await queue.EnqueueAsync(CreateOwnedBatch(3))).IsAccepted);
        using var whileWaiting = new CancellationTokenSource();
        var pending = queue.EnqueueAsync(CreateOwnedBatch(5), cancellationToken: whileWaiting.Token).AsTask();
        await Task.Delay(50);
        await whileWaiting.CancelAsync();
        var canceledWhileWaiting = await pending;

        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Canceled, canceledWhileWaiting.Status);
        Assert.Equal(1, queue.PendingCount);
        Assert.Equal(2, queue.CreateTelemetrySummary().EnqueueCanceledCount);
    }
}
