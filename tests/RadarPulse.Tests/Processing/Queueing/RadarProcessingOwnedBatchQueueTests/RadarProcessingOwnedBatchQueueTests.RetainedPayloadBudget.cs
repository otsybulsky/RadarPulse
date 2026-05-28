using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingOwnedBatchQueueTests
{
    [Fact]
    public async Task ReturnFullModeRejectsWhenRetainedPayloadByteBudgetIsExhausted()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(
                capacity: 3,
                fullMode: RadarProcessingProviderQueueFullMode.ReturnFull,
                maxRetainedPayloadBytes: 3));

        var first = await queue.EnqueueAsync(CreateOwnedBatch(1));
        var second = await queue.EnqueueAsync(CreateOwnedBatch(3));

        Assert.True(first.IsAccepted);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Full, second.Status);
        Assert.Contains("retained payload byte budget", second.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, queue.PendingCount);
        Assert.Equal(2, queue.PendingPayloadBytes);
        Assert.Equal(2, queue.PendingRetainedPayloadBytes);

        var dequeued = await queue.DequeueAsync();
        var third = await queue.EnqueueAsync(CreateOwnedBatch(5));
        var summary = queue.CreateTelemetrySummary();

        Assert.True(dequeued.HasItem);
        Assert.True(third.IsAccepted);
        Assert.Equal(1, third.Sequence?.Value);
        Assert.Equal(1, summary.EnqueueFullCount);
        Assert.Equal(2, summary.QueuedPayloadBytesHighWatermark);
        Assert.Equal(2, summary.RetainedPayloadBytesHighWatermark);
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
    }

    [Fact]
    public async Task WaitModeAcceptsBlockedEnqueueAfterRetainedPayloadBudgetFrees()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(
                capacity: 2,
                fullMode: RadarProcessingProviderQueueFullMode.Wait,
                enqueueTimeout: TimeSpan.FromSeconds(5),
                maxRetainedPayloadBytes: 2));

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
        Assert.Equal(2, queue.PendingRetainedPayloadBytes);
        Assert.True(second.EnqueueWaitTime > TimeSpan.Zero);
        Assert.True(queue.CreateTelemetrySummary().TotalEnqueueWaitTime > TimeSpan.Zero);
    }

    [Fact]
    public async Task WaitModeCanTimeoutWhileRetainedPayloadByteBudgetIsExhausted()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(
                capacity: 2,
                fullMode: RadarProcessingProviderQueueFullMode.Wait,
                enqueueTimeout: TimeSpan.FromMilliseconds(20),
                maxRetainedPayloadBytes: 2));

        var first = await queue.EnqueueAsync(CreateOwnedBatch(1));
        var second = await queue.EnqueueAsync(CreateOwnedBatch(3));

        Assert.True(first.IsAccepted);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.TimedOut, second.Status);
        Assert.Contains("retained payload byte budget", second.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, queue.PendingCount);

        var summary = queue.CreateTelemetrySummary();

        Assert.Equal(2, summary.EnqueueAttemptCount);
        Assert.Equal(1, summary.EnqueuedBatchCount);
        Assert.Equal(1, summary.EnqueueTimedOutCount);
        Assert.True(summary.HasBackpressure);
    }

    [Fact]
    public async Task OversizedRetainedPayloadIsRejectedWithoutWaiting()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(
                capacity: 2,
                fullMode: RadarProcessingProviderQueueFullMode.Wait,
                enqueueTimeout: TimeSpan.FromSeconds(5),
                maxRetainedPayloadBytes: 3));

        var result = await queue.EnqueueAsync(CreateOwnedBatch([1, 2, 3, 4]));

        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Full, result.Status);
        Assert.Contains("exceed", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, queue.PendingCount);
        Assert.Equal(0, queue.PendingRetainedPayloadBytes);
        Assert.Equal(1, queue.CreateTelemetrySummary().EnqueueFullCount);
    }

    [Fact]
    public async Task DisposeReleasesRetainedPayloadByteBudgetWaiters()
    {
        var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(
                capacity: 2,
                fullMode: RadarProcessingProviderQueueFullMode.Wait,
                enqueueTimeout: TimeSpan.FromSeconds(5),
                maxRetainedPayloadBytes: 2));

        Assert.True((await queue.EnqueueAsync(CreateOwnedBatch(1))).IsAccepted);
        var pending = queue.EnqueueAsync(CreateOwnedBatch(3)).AsTask();

        await Task.Delay(50);
        Assert.False(pending.IsCompleted);

        queue.Dispose();
        var result = await pending;

        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Closed, result.Status);
        Assert.Equal(0, queue.PendingCount);
        Assert.Equal(0, queue.PendingRetainedPayloadBytes);
    }
}
