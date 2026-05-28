using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingOwnedBatchQueueTests
{
    [Fact]
    public async Task QueueAssignsMonotonicSequencesAndDequeuesInFifoOrder()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 2));

        var first = await queue.EnqueueAsync(CreateOwnedBatch(1));
        var second = await queue.EnqueueAsync(CreateOwnedBatch(3));

        Assert.True(first.IsAccepted);
        Assert.True(second.IsAccepted);
        Assert.Equal(0, first.Sequence?.Value);
        Assert.Equal(1, second.Sequence?.Value);
        Assert.Equal(2, queue.PendingCount);
        Assert.Equal(4, queue.PendingPayloadBytes);

        var firstDequeued = await queue.DequeueAsync();
        var secondDequeued = await queue.DequeueAsync();

        Assert.True(firstDequeued.HasItem);
        Assert.True(secondDequeued.HasItem);
        Assert.Equal(0, firstDequeued.Batch!.Sequence.Value);
        Assert.Equal(1, secondDequeued.Batch!.Sequence.Value);
        Assert.Equal([1, 2], firstDequeued.Batch.Batch.Payload.ToArray());
        Assert.Equal([3, 4], secondDequeued.Batch.Batch.Payload.ToArray());
        Assert.Equal(0, queue.PendingCount);
        Assert.Equal(0, queue.PendingPayloadBytes);
    }

    [Fact]
    public async Task AcceptedCallbackRunsBeforeWaitingDequeueReturns()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 1));
        using var callbackEntered = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();

        var pendingDequeue = queue.DequeueAsync().AsTask();
        var pendingEnqueue = Task.Run(async () =>
            await queue.EnqueueAsync(
                CreateOwnedBatch(1),
                onAccepted: queuedBatch =>
                {
                    Assert.Equal(0, queuedBatch.Sequence.Value);
                    callbackEntered.Set();
                    Assert.True(releaseCallback.Wait(TimeSpan.FromSeconds(5)));
                }));

        Assert.True(callbackEntered.Wait(TimeSpan.FromSeconds(5)));
        Assert.False(pendingDequeue.IsCompleted);

        releaseCallback.Set();
        var enqueue = await pendingEnqueue.WaitAsync(TimeSpan.FromSeconds(5));
        var dequeue = await pendingDequeue.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(enqueue.IsAccepted);
        Assert.True(dequeue.HasItem);
        Assert.Equal(enqueue.Sequence, dequeue.Batch!.Sequence);
    }

    [Fact]
    public async Task QueueRejectsLeasedBatchWithoutRetainingIt()
    {
        using var queue = new RadarProcessingOwnedBatchQueue();
        var builder = CreateBatchBuilder(1);

        builder.ConsumeLeased(batch =>
        {
            Assert.Equal(RadarEventBatchLifetime.Leased, batch.Lifetime);
            Assert.Throws<ArgumentException>(() =>
                queue.EnqueueAsync(batch).AsTask().GetAwaiter().GetResult());
        });

        Assert.Equal(0, queue.PendingCount);
        Assert.Equal(0, queue.CreateTelemetrySummary().EnqueueAttemptCount);
    }
}
