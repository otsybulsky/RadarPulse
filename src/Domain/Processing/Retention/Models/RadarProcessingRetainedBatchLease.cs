using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingRetainedBatchLease : IDisposable
{
    private bool disposed;

    internal RadarProcessingRetainedBatchLease(
        RadarProcessingRetainedQueuedBatch retainedBatch)
    {
        RetainedBatch = retainedBatch ?? throw new ArgumentNullException(nameof(retainedBatch));
    }

    public RadarProcessingRetainedQueuedBatch RetainedBatch { get; }

    public RadarProcessingQueuedBatch QueuedBatch => RetainedBatch.QueuedBatch;

    public RadarEventBatch Batch => QueuedBatch.Batch;

    public bool IsDisposed => disposed;

    public RadarProcessingRetainedPayloadReleaseResult Release()
    {
        var result = RetainedBatch.Resource.Release();
        disposed = true;
        return result;
    }

    public void Dispose()
    {
        if (!disposed)
        {
            Release();
        }
    }
}
