namespace RadarPulse.Domain.Processing;

/// <summary>
/// Queued batch paired with its retained payload resource.
/// </summary>
public sealed class RadarProcessingRetainedQueuedBatch
{
    /// <summary>
    /// Creates a retained queued batch and transfers resource ownership to the queue.
    /// </summary>
    public RadarProcessingRetainedQueuedBatch(
        RadarProcessingQueuedBatch queuedBatch,
        RadarProcessingRetainedBatchResource? resource = null)
    {
        QueuedBatch = queuedBatch ?? throw new ArgumentNullException(nameof(queuedBatch));
        Resource = resource ?? RadarProcessingRetainedBatchResource.NotRequired();
        Resource.TransferToQueue();
    }

    /// <summary>
    /// Queued batch with owned payload.
    /// </summary>
    public RadarProcessingQueuedBatch QueuedBatch { get; }

    /// <summary>
    /// Retained payload resource associated with the queued batch.
    /// </summary>
    public RadarProcessingRetainedBatchResource Resource { get; }

    /// <summary>
    /// Provider ordering key for the queued batch.
    /// </summary>
    public RadarProcessingQueuedBatchSequence Sequence => QueuedBatch.Sequence;

    /// <summary>
    /// Indicates whether the retained resource is already terminal.
    /// </summary>
    public bool HasTerminalResource => Resource.IsTerminal;

    /// <summary>
    /// Transfers resource ownership to a consumer lease.
    /// </summary>
    public RadarProcessingRetainedBatchLease AcquireForConsumer()
    {
        Resource.TransferToConsumer();
        return new RadarProcessingRetainedBatchLease(this);
    }

    /// <summary>
    /// Releases a queue-owned resource that never reached a consumer.
    /// </summary>
    public RadarProcessingRetainedPayloadReleaseResult ReleasePending() =>
        Resource.Release();
}
