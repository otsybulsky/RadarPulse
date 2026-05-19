using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingWorkerMailboxTests
{
    [Fact]
    public void MailboxStatusEnumValuesAreStable()
    {
        Assert.Equal(1, (int)RadarProcessingWorkerMailboxEnqueueStatus.Accepted);
        Assert.Equal(2, (int)RadarProcessingWorkerMailboxEnqueueStatus.Full);
        Assert.Equal(3, (int)RadarProcessingWorkerMailboxEnqueueStatus.Closed);
        Assert.Equal(4, (int)RadarProcessingWorkerMailboxEnqueueStatus.Disposed);

        Assert.Equal(1, (int)RadarProcessingWorkerMailboxDequeueStatus.Item);
        Assert.Equal(2, (int)RadarProcessingWorkerMailboxDequeueStatus.Closed);
        Assert.Equal(3, (int)RadarProcessingWorkerMailboxDequeueStatus.Canceled);
        Assert.Equal(4, (int)RadarProcessingWorkerMailboxDequeueStatus.Disposed);
    }

    [Fact]
    public void OptionsRejectInvalidCapacity()
    {
        Assert.Equal(1, RadarProcessingWorkerMailboxOptions.Default.Capacity);
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarProcessingWorkerMailboxOptions(0));
    }

    [Fact]
    public void ResultContractsRejectInvalidShapes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingWorkerMailboxEnqueueResult((RadarProcessingWorkerMailboxEnqueueStatus)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingWorkerMailboxDequeueResult<string>((RadarProcessingWorkerMailboxDequeueStatus)255));
        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingWorkerMailboxDequeueResult<string>(RadarProcessingWorkerMailboxDequeueStatus.Item));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingWorkerMailboxDequeueResult<string>(
                RadarProcessingWorkerMailboxDequeueStatus.Closed,
                "unexpected"));
    }

    [Fact]
    public async Task MailboxEnforcesCapacityAndReusesSpaceAfterDequeue()
    {
        using var mailbox = new RadarProcessingWorkerMailbox<string>(
            new RadarProcessingWorkerMailboxOptions(capacity: 1));

        var first = mailbox.TryEnqueue("first");
        var second = mailbox.TryEnqueue("second");

        Assert.True(first.IsAccepted);
        Assert.Equal(RadarProcessingWorkerMailboxEnqueueStatus.Full, second.Status);
        Assert.Equal(1, mailbox.PendingCount);

        var dequeued = await mailbox.DequeueAsync();

        Assert.True(dequeued.HasItem);
        Assert.Equal("first", dequeued.Item);
        Assert.Equal(0, mailbox.PendingCount);

        var third = mailbox.TryEnqueue("third");

        Assert.True(third.IsAccepted);
        Assert.Equal(1, mailbox.PendingCount);
    }

    [Fact]
    public async Task MailboxDequeuesInFifoOrder()
    {
        using var mailbox = new RadarProcessingWorkerMailbox<string>(
            new RadarProcessingWorkerMailboxOptions(capacity: 3));

        Assert.True(mailbox.TryEnqueue("first").IsAccepted);
        Assert.True(mailbox.TryEnqueue("second").IsAccepted);
        Assert.True(mailbox.TryEnqueue("third").IsAccepted);

        Assert.Equal("first", (await mailbox.DequeueAsync()).Item);
        Assert.Equal("second", (await mailbox.DequeueAsync()).Item);
        Assert.Equal("third", (await mailbox.DequeueAsync()).Item);
        Assert.Equal(0, mailbox.PendingCount);
    }

    [Fact]
    public async Task CloseRejectsNewEnqueueButAllowsDrainingAcceptedItems()
    {
        using var mailbox = new RadarProcessingWorkerMailbox<string>(
            new RadarProcessingWorkerMailboxOptions(capacity: 2));

        Assert.True(mailbox.TryEnqueue("first").IsAccepted);
        Assert.True(mailbox.TryEnqueue("second").IsAccepted);

        mailbox.Close();
        var enqueueAfterClose = mailbox.TryEnqueue("third");

        Assert.True(mailbox.IsClosed);
        Assert.Equal(RadarProcessingWorkerMailboxEnqueueStatus.Closed, enqueueAfterClose.Status);
        Assert.Equal("first", (await mailbox.DequeueAsync()).Item);
        Assert.Equal("second", (await mailbox.DequeueAsync()).Item);

        var closed = await mailbox.DequeueAsync();

        Assert.Equal(RadarProcessingWorkerMailboxDequeueStatus.Closed, closed.Status);
        Assert.False(closed.HasItem);
        Assert.Equal(0, mailbox.PendingCount);
    }

    [Fact]
    public async Task DequeueSupportsCancellationWhileWaiting()
    {
        using var mailbox = new RadarProcessingWorkerMailbox<string>();
        using var cancellation = new CancellationTokenSource();

        var pending = mailbox.DequeueAsync(cancellation.Token).AsTask();
        await cancellation.CancelAsync();
        var result = await pending;

        Assert.Equal(RadarProcessingWorkerMailboxDequeueStatus.Canceled, result.Status);
        Assert.False(result.HasItem);
        Assert.Equal(0, mailbox.PendingCount);
    }

    [Fact]
    public async Task DisposeRejectsEnqueueAndDequeueAndClearsPendingItems()
    {
        var mailbox = new RadarProcessingWorkerMailbox<string>(
            new RadarProcessingWorkerMailboxOptions(capacity: 2));

        Assert.True(mailbox.TryEnqueue("first").IsAccepted);
        Assert.True(mailbox.TryEnqueue("second").IsAccepted);
        Assert.Equal(2, mailbox.PendingCount);

        mailbox.Dispose();
        var enqueue = mailbox.TryEnqueue("third");
        var dequeue = await mailbox.DequeueAsync();

        Assert.True(mailbox.IsDisposed);
        Assert.True(mailbox.IsClosed);
        Assert.Equal(RadarProcessingWorkerMailboxEnqueueStatus.Disposed, enqueue.Status);
        Assert.Equal(RadarProcessingWorkerMailboxDequeueStatus.Disposed, dequeue.Status);
        Assert.Equal(0, mailbox.PendingCount);
    }

    [Fact]
    public async Task DisposeReleasesWaitingDequeue()
    {
        var mailbox = new RadarProcessingWorkerMailbox<string>();

        var pending = mailbox.DequeueAsync().AsTask();
        mailbox.Dispose();
        var result = await pending;

        Assert.Equal(RadarProcessingWorkerMailboxDequeueStatus.Disposed, result.Status);
        Assert.False(result.HasItem);
    }

    [Fact]
    public void MailboxRejectsNullWork()
    {
        using var mailbox = new RadarProcessingWorkerMailbox<string>();

        Assert.Throws<ArgumentNullException>(() => mailbox.TryEnqueue(null!));
    }
}
