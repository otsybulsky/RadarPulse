using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingOwnedBatchQueueTests
{
    [Fact]
    public async Task CloseRejectsNewEnqueueButAllowsAcceptedBatchesToDrain()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 2));

        Assert.True((await queue.EnqueueAsync(CreateOwnedBatch(1))).IsAccepted);
        Assert.True((await queue.EnqueueAsync(CreateOwnedBatch(3))).IsAccepted);

        queue.Close();
        var rejected = await queue.EnqueueAsync(CreateOwnedBatch(5));

        Assert.True(queue.IsClosed);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Closed, rejected.Status);
        Assert.Equal(2, queue.PendingCount);

        Assert.True((await queue.DequeueAsync()).HasItem);
        Assert.True((await queue.DequeueAsync()).HasItem);

        var closed = await queue.DequeueAsync();

        Assert.Equal(RadarProcessingOwnedBatchDequeueStatus.Closed, closed.Status);
        Assert.False(closed.HasItem);
        Assert.Equal(0, queue.PendingCount);
        Assert.Equal(1, queue.CreateTelemetrySummary().EnqueueClosedCount);
    }

    [Fact]
    public async Task CancelQueuedRejectsNewEnqueueAndClearsAcceptedBatches()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 2));

        Assert.True((await queue.EnqueueAsync(CreateOwnedBatch(1))).IsAccepted);
        Assert.True((await queue.EnqueueAsync(CreateOwnedBatch(3))).IsAccepted);

        var canceled = queue.CancelQueued();
        var rejected = await queue.EnqueueAsync(CreateOwnedBatch(5));
        var closed = await queue.DequeueAsync();
        var summary = queue.CreateTelemetrySummary();

        Assert.True(queue.IsClosed);
        Assert.Equal([0L, 1L], canceled.Select(static batch => batch.Sequence.Value).ToArray());
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Closed, rejected.Status);
        Assert.Equal(RadarProcessingOwnedBatchDequeueStatus.Closed, closed.Status);
        Assert.Equal(0, queue.PendingCount);
        Assert.Equal(0, queue.PendingPayloadBytes);
        Assert.Equal(2, summary.EnqueuedBatchCount);
        Assert.Equal(0, summary.DequeuedBatchCount);
        Assert.Equal(1, summary.EnqueueClosedCount);
        Assert.Equal(0, summary.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, summary.CurrentPendingRetainedPayloadBytes);
    }

    [Fact]
    public async Task FaultRejectsLaterEnqueueAndReportsFaultAfterDrain()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 1));

        Assert.True((await queue.EnqueueAsync(CreateOwnedBatch(1))).IsAccepted);

        queue.Fault("processing failed");
        var rejected = await queue.EnqueueAsync(CreateOwnedBatch(3));
        var drained = await queue.DequeueAsync();
        var faulted = await queue.DequeueAsync();

        Assert.True(queue.IsClosed);
        Assert.True(queue.IsFaulted);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Faulted, rejected.Status);
        Assert.Equal("processing failed", rejected.Message);
        Assert.True(drained.HasItem);
        Assert.Equal(RadarProcessingOwnedBatchDequeueStatus.Faulted, faulted.Status);
        Assert.Equal("processing failed", faulted.Message);
        Assert.Equal(1, queue.CreateTelemetrySummary().EnqueueFaultedCount);
    }

    [Fact]
    public async Task DequeueSupportsCancellationWhileWaiting()
    {
        using var queue = new RadarProcessingOwnedBatchQueue();
        using var cancellation = new CancellationTokenSource();

        var pending = queue.DequeueAsync(cancellation.Token).AsTask();
        await cancellation.CancelAsync();
        var result = await pending;

        Assert.Equal(RadarProcessingOwnedBatchDequeueStatus.Canceled, result.Status);
        Assert.False(result.HasItem);
    }

    [Fact]
    public async Task DisposeRejectsEnqueueAndDequeueAndClearsPendingBatches()
    {
        var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 2));

        Assert.True((await queue.EnqueueAsync(CreateOwnedBatch(1))).IsAccepted);
        Assert.True((await queue.EnqueueAsync(CreateOwnedBatch(3))).IsAccepted);
        Assert.Equal(2, queue.PendingCount);

        queue.Dispose();
        var enqueue = await queue.EnqueueAsync(CreateOwnedBatch(5));
        var dequeue = await queue.DequeueAsync();

        Assert.True(queue.IsDisposed);
        Assert.True(queue.IsClosed);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Closed, enqueue.Status);
        Assert.Equal(RadarProcessingOwnedBatchDequeueStatus.Disposed, dequeue.Status);
        Assert.Equal(0, queue.PendingCount);
        Assert.Equal(0, queue.PendingPayloadBytes);
        Assert.Equal(0, queue.CreateTelemetrySummary().DequeuedBatchCount);
    }
}
