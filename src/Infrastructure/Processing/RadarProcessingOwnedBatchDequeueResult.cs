using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingOwnedBatchDequeueResult
{
    public RadarProcessingOwnedBatchDequeueResult(
        RadarProcessingOwnedBatchDequeueStatus status,
        RadarProcessingQueuedBatch? batch = null,
        string message = "")
    {
        EnsureKnownStatus(status);
        ArgumentNullException.ThrowIfNull(message);

        if (status == RadarProcessingOwnedBatchDequeueStatus.Item)
        {
            ArgumentNullException.ThrowIfNull(batch);
        }
        else if (batch is not null)
        {
            throw new ArgumentException("Non-item dequeue results must not carry a batch.", nameof(batch));
        }

        Status = status;
        Batch = batch;
        Message = message;
    }

    public RadarProcessingOwnedBatchDequeueStatus Status { get; }

    public RadarProcessingQueuedBatch? Batch { get; }

    public string Message { get; }

    public bool HasItem => Status == RadarProcessingOwnedBatchDequeueStatus.Item;

    internal static void EnsureKnownStatus(
        RadarProcessingOwnedBatchDequeueStatus status)
    {
        if (status is not RadarProcessingOwnedBatchDequeueStatus.Item and
            not RadarProcessingOwnedBatchDequeueStatus.Closed and
            not RadarProcessingOwnedBatchDequeueStatus.Canceled and
            not RadarProcessingOwnedBatchDequeueStatus.Faulted and
            not RadarProcessingOwnedBatchDequeueStatus.Disposed)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
    }
}
