using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Result returned by owned provider queue dequeue operations.
/// </summary>
/// <remarks>
/// Item results always carry a queued batch. Terminal, fault, cancellation, and
/// disposal results carry no batch so sessions can map them directly to a
/// session status without double-processing retained payloads.
/// </remarks>
public sealed record RadarProcessingOwnedBatchDequeueResult
{
    /// <summary>
    /// Creates a dequeue result and enforces batch/status consistency.
    /// </summary>
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

    /// <summary>
    /// Queue reader outcome.
    /// </summary>
    public RadarProcessingOwnedBatchDequeueStatus Status { get; }

    /// <summary>
    /// Queued batch returned for item results.
    /// </summary>
    public RadarProcessingQueuedBatch? Batch { get; }

    /// <summary>
    /// Optional terminal or fault message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Indicates whether the dequeue operation produced a queued batch.
    /// </summary>
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
