using System.Threading.Channels;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingWorkerMailbox<TWork> : IDisposable
    where TWork : class
{
    private readonly Channel<TWork> channel;
    private int pendingCount;
    private int closed;
    private int disposed;

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

    public RadarProcessingWorkerMailboxOptions Options { get; }

    public int PendingCount => Volatile.Read(ref pendingCount);

    public bool IsClosed => Volatile.Read(ref closed) != 0;

    public bool IsDisposed => Volatile.Read(ref disposed) != 0;

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

    public void Close()
    {
        if (Interlocked.Exchange(ref closed, 1) == 0)
        {
            channel.Writer.TryComplete();
        }
    }

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
