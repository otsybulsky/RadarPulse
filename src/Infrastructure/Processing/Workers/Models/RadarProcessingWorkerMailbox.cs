using System.Threading.Channels;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Bounded single-reader mailbox used to feed one async processing worker.
/// </summary>
/// <remarks>
/// Writers use <see cref="TryEnqueue"/> for a non-blocking backpressure signal.
/// The mailbox distinguishes close from disposal so worker-group drain logic can
/// finish accepted work while still rejecting new submissions.
/// </remarks>
public sealed class RadarProcessingWorkerMailbox<TWork> : IDisposable
    where TWork : class
{
    private readonly Channel<TWork> channel;
    private int pendingCount;
    private int closed;
    private int disposed;

    /// <summary>
    /// Creates a mailbox over a bounded channel with one reader and multiple writers.
    /// </summary>
    public RadarProcessingWorkerMailbox(
        RadarProcessingWorkerMailboxOptions? options = null)
    {
        Options = options ?? RadarProcessingWorkerMailboxOptions.Default;
        channel = Channel.CreateBounded<TWork>(
            new BoundedChannelOptions(Options.Capacity)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
    }

    /// <summary>
    /// Effective bounded-capacity settings for the mailbox.
    /// </summary>
    public RadarProcessingWorkerMailboxOptions Options { get; }

    /// <summary>
    /// Number of accepted items that have not yet been removed by the reader.
    /// </summary>
    public int PendingCount => Volatile.Read(ref pendingCount);

    /// <summary>
    /// Indicates whether writers have been closed.
    /// </summary>
    public bool IsClosed => Volatile.Read(ref closed) != 0;

    /// <summary>
    /// Indicates whether the mailbox has been disposed and drained.
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref disposed) != 0;

    /// <summary>
    /// Attempts to enqueue work without waiting for bounded-channel capacity.
    /// </summary>
    /// <returns>
    /// Accepted when the channel takes the item; otherwise a closed, disposed, or
    /// full status that lets the dispatcher decide the batch outcome.
    /// </returns>
    public RadarProcessingWorkerMailboxEnqueueResult TryEnqueue(TWork work)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (IsDisposed)
        {
            return new RadarProcessingWorkerMailboxEnqueueResult(
                RadarProcessingWorkerMailboxEnqueueStatus.Disposed);
        }

        if (IsClosed)
        {
            return new RadarProcessingWorkerMailboxEnqueueResult(
                RadarProcessingWorkerMailboxEnqueueStatus.Closed);
        }

        Interlocked.Increment(ref pendingCount);
        if (!channel.Writer.TryWrite(work))
        {
            Interlocked.Decrement(ref pendingCount);
            return new RadarProcessingWorkerMailboxEnqueueResult(
                IsDisposed
                    ? RadarProcessingWorkerMailboxEnqueueStatus.Disposed
                    : IsClosed
                        ? RadarProcessingWorkerMailboxEnqueueStatus.Closed
                        : RadarProcessingWorkerMailboxEnqueueStatus.Full);
        }

        return new RadarProcessingWorkerMailboxEnqueueResult(
            RadarProcessingWorkerMailboxEnqueueStatus.Accepted);
    }

    /// <summary>
    /// Waits for the next accepted work item or for mailbox shutdown/cancellation.
    /// </summary>
    /// <returns>
    /// An item result when work is available; otherwise a cancellation, close, or
    /// disposal status with no item attached.
    /// </returns>
    public async ValueTask<RadarProcessingWorkerMailboxDequeueResult<TWork>> DequeueAsync(
        CancellationToken cancellationToken = default)
    {
        if (IsDisposed)
        {
            return new RadarProcessingWorkerMailboxDequeueResult<TWork>(
                RadarProcessingWorkerMailboxDequeueStatus.Disposed);
        }

        try
        {
            var item = await channel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            Interlocked.Decrement(ref pendingCount);

            if (IsDisposed)
            {
                return new RadarProcessingWorkerMailboxDequeueResult<TWork>(
                    RadarProcessingWorkerMailboxDequeueStatus.Disposed);
            }

            return new RadarProcessingWorkerMailboxDequeueResult<TWork>(
                RadarProcessingWorkerMailboxDequeueStatus.Item,
                item);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new RadarProcessingWorkerMailboxDequeueResult<TWork>(
                RadarProcessingWorkerMailboxDequeueStatus.Canceled);
        }
        catch (ChannelClosedException)
        {
            return new RadarProcessingWorkerMailboxDequeueResult<TWork>(
                IsDisposed
                    ? RadarProcessingWorkerMailboxDequeueStatus.Disposed
                    : RadarProcessingWorkerMailboxDequeueStatus.Closed);
        }
    }

    /// <summary>
    /// Closes the writer side while allowing the reader to drain already accepted items.
    /// </summary>
    public void Close()
    {
        if (Interlocked.Exchange(ref closed, 1) == 0)
        {
            channel.Writer.TryComplete();
        }
    }

    /// <summary>
    /// Permanently closes the mailbox and drops any buffered items.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref closed, 1);
        channel.Writer.TryComplete();
        while (channel.Reader.TryRead(out _))
        {
            Interlocked.Decrement(ref pendingCount);
        }
    }
}
