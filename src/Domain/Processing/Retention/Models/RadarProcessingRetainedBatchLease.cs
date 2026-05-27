using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

/// <summary>
/// Consumer lease that releases a retained queued batch resource on dispose.
/// </summary>
public sealed class RadarProcessingRetainedBatchLease : IDisposable
{
    private bool disposed;

    internal RadarProcessingRetainedBatchLease(
        RadarProcessingRetainedQueuedBatch retainedBatch)
    {
        RetainedBatch = retainedBatch ?? throw new ArgumentNullException(nameof(retainedBatch));
    }

    /// <summary>
    /// Retained queued batch owned by the lease.
    /// </summary>
    public RadarProcessingRetainedQueuedBatch RetainedBatch { get; }

    /// <summary>
    /// Queued batch exposed to the consumer.
    /// </summary>
    public RadarProcessingQueuedBatch QueuedBatch => RetainedBatch.QueuedBatch;

    /// <summary>
    /// Owned radar event batch payload exposed to the consumer.
    /// </summary>
    public RadarEventBatch Batch => QueuedBatch.Batch;

    /// <summary>
    /// Indicates whether the lease has released its retained resource.
    /// </summary>
    public bool IsDisposed => disposed;

    /// <summary>
    /// Releases the retained resource and marks the lease disposed.
    /// </summary>
    public RadarProcessingRetainedPayloadReleaseResult Release()
    {
        var result = RetainedBatch.Resource.Release();
        disposed = true;
        return result;
    }

    /// <summary>
    /// Releases the retained resource when the lease is disposed.
    /// </summary>
    public void Dispose()
    {
        if (!disposed)
        {
            Release();
        }
    }
}
