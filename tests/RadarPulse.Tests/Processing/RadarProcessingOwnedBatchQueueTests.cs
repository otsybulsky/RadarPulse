using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingOwnedBatchQueueTests
{
    [Fact]
    public void OwnedBatchDequeueContractsRejectInvalidShapes()
    {
        Assert.Equal(1, (int)RadarProcessingOwnedBatchDequeueStatus.Item);
        Assert.Equal(2, (int)RadarProcessingOwnedBatchDequeueStatus.Closed);
        Assert.Equal(3, (int)RadarProcessingOwnedBatchDequeueStatus.Canceled);
        Assert.Equal(4, (int)RadarProcessingOwnedBatchDequeueStatus.Faulted);
        Assert.Equal(5, (int)RadarProcessingOwnedBatchDequeueStatus.Disposed);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingOwnedBatchDequeueResult((RadarProcessingOwnedBatchDequeueStatus)255));
        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingOwnedBatchDequeueResult(RadarProcessingOwnedBatchDequeueStatus.Item));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingOwnedBatchDequeueResult(
                RadarProcessingOwnedBatchDequeueStatus.Closed,
                new RadarProcessingQueuedBatch(RadarProcessingQueuedBatchSequence.Initial, CreateOwnedBatch(1))));
        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingOwnedBatchDequeueResult(
                RadarProcessingOwnedBatchDequeueStatus.Closed,
                message: null!));
    }

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

    private static RadarEventBatch CreateOwnedBatch(byte firstPayloadValue) =>
        CreateOwnedBatch([firstPayloadValue, (byte)(firstPayloadValue + 1)]);

    private static RadarEventBatch CreateOwnedBatch(byte[] payload) =>
        CreateBatchBuilder(payload).Build();

    private static RadarEventBatchBuilder CreateBatchBuilder(byte firstPayloadValue)
        => CreateBatchBuilder([firstPayloadValue, (byte)(firstPayloadValue + 1)]);

    private static RadarEventBatchBuilder CreateBatchBuilder(byte[] payload)
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
            gateCount: (ushort)payload.Length,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: payload);

        return builder;
    }
}
