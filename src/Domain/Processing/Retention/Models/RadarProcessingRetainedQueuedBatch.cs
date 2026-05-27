namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingRetainedQueuedBatch
{
    public RadarProcessingRetainedQueuedBatch(
        RadarProcessingQueuedBatch queuedBatch,
        RadarProcessingRetainedBatchResource? resource = null)
    {
        QueuedBatch = queuedBatch ?? throw new ArgumentNullException(nameof(queuedBatch));
        Resource = resource ?? RadarProcessingRetainedBatchResource.NotRequired();
        Resource.TransferToQueue();
    }

    public RadarProcessingQueuedBatch QueuedBatch { get; }

    public RadarProcessingRetainedBatchResource Resource { get; }

    public RadarProcessingQueuedBatchSequence Sequence => QueuedBatch.Sequence;

    public bool HasTerminalResource => Resource.IsTerminal;

    public RadarProcessingRetainedBatchLease AcquireForConsumer()
    {
        Resource.TransferToConsumer();
        return new RadarProcessingRetainedBatchLease(this);
    }

    public RadarProcessingRetainedPayloadReleaseResult ReleasePending() =>
        Resource.Release();
}
