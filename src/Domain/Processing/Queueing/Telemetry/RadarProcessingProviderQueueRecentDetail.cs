namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingProviderQueueRecentDetail
{
    public RadarProcessingProviderQueueRecentDetail(
        RadarProcessingProviderQueueRecentDetailKind kind,
        RadarProcessingQueuedBatchSequence? sequence = null,
        RadarProcessingQueuedBatchEnqueueStatus? enqueueStatus = null,
        RadarProcessingQueuedBatchProcessingStatus? processingStatus = null,
        int streamEventCount = 0,
        long payloadBytes = 0,
        long payloadValueCount = 0,
        TimeSpan ownedSnapshotTime = default,
        long ownedSnapshotAllocatedBytes = 0,
        TimeSpan enqueueWaitTime = default,
        TimeSpan providerToProcessingLatency = default,
        int queueDepth = 0,
        long queuedPayloadBytes = 0,
        string message = "")
    {
        EnsureKnownKind(kind);
        if (enqueueStatus.HasValue)
        {
            RadarProcessingQueuedBatchEnqueueResult.EnsureKnownStatus(enqueueStatus.Value);
        }

        if (processingStatus.HasValue)
        {
            RadarProcessingQueuedBatchProcessingResult.EnsureKnownStatus(processingStatus.Value);
        }

        ArgumentOutOfRangeException.ThrowIfNegative(streamEventCount);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadValueCount);
        EnsureNonNegative(ownedSnapshotTime, nameof(ownedSnapshotTime));
        ArgumentOutOfRangeException.ThrowIfNegative(ownedSnapshotAllocatedBytes);
        EnsureNonNegative(enqueueWaitTime, nameof(enqueueWaitTime));
        EnsureNonNegative(providerToProcessingLatency, nameof(providerToProcessingLatency));
        ArgumentOutOfRangeException.ThrowIfNegative(queueDepth);
        ArgumentOutOfRangeException.ThrowIfNegative(queuedPayloadBytes);
        ArgumentNullException.ThrowIfNull(message);

        Kind = kind;
        Sequence = sequence;
        EnqueueStatus = enqueueStatus;
        ProcessingStatus = processingStatus;
        StreamEventCount = streamEventCount;
        PayloadBytes = payloadBytes;
        PayloadValueCount = payloadValueCount;
        OwnedSnapshotTime = ownedSnapshotTime;
        OwnedSnapshotAllocatedBytes = ownedSnapshotAllocatedBytes;
        EnqueueWaitTime = enqueueWaitTime;
        ProviderToProcessingLatency = providerToProcessingLatency;
        QueueDepth = queueDepth;
        QueuedPayloadBytes = queuedPayloadBytes;
        Message = message;
    }

    public RadarProcessingProviderQueueRecentDetailKind Kind { get; }

    public RadarProcessingQueuedBatchSequence? Sequence { get; }

    public RadarProcessingQueuedBatchEnqueueStatus? EnqueueStatus { get; }

    public RadarProcessingQueuedBatchProcessingStatus? ProcessingStatus { get; }

    public int StreamEventCount { get; }

    public long PayloadBytes { get; }

    public long PayloadValueCount { get; }

    public TimeSpan OwnedSnapshotTime { get; }

    public long OwnedSnapshotAllocatedBytes { get; }

    public TimeSpan EnqueueWaitTime { get; }

    public TimeSpan ProviderToProcessingLatency { get; }

    public int QueueDepth { get; }

    public long QueuedPayloadBytes { get; }

    public string Message { get; }

    public static RadarProcessingProviderQueueRecentDetail FromEnqueueResult(
        RadarProcessingQueuedBatchEnqueueResult result,
        int queueDepth = 0,
        long queuedPayloadBytes = 0)
    {
        ArgumentNullException.ThrowIfNull(result);
        var batch = result.Batch;
        return new RadarProcessingProviderQueueRecentDetail(
            RadarProcessingProviderQueueRecentDetailKind.Enqueue,
            result.Sequence,
            enqueueStatus: result.Status,
            streamEventCount: batch?.StreamEventCount ?? 0,
            payloadBytes: batch?.PayloadBytes ?? 0,
            payloadValueCount: batch?.PayloadValueCount ?? 0,
            ownedSnapshotTime: batch?.OwnedSnapshotTime ?? default,
            ownedSnapshotAllocatedBytes: batch?.OwnedSnapshotAllocatedBytes ?? 0,
            enqueueWaitTime: result.EnqueueWaitTime,
            queueDepth: queueDepth,
            queuedPayloadBytes: queuedPayloadBytes,
            message: result.Message);
    }

    public static RadarProcessingProviderQueueRecentDetail FromDequeuedBatch(
        RadarProcessingQueuedBatch batch,
        TimeSpan providerToProcessingLatency,
        int queueDepth = 0,
        long queuedPayloadBytes = 0)
    {
        ArgumentNullException.ThrowIfNull(batch);
        return new RadarProcessingProviderQueueRecentDetail(
            RadarProcessingProviderQueueRecentDetailKind.Dequeue,
            batch.Sequence,
            streamEventCount: batch.StreamEventCount,
            payloadBytes: batch.PayloadBytes,
            payloadValueCount: batch.PayloadValueCount,
            ownedSnapshotTime: batch.OwnedSnapshotTime,
            ownedSnapshotAllocatedBytes: batch.OwnedSnapshotAllocatedBytes,
            providerToProcessingLatency: providerToProcessingLatency,
            queueDepth: queueDepth,
            queuedPayloadBytes: queuedPayloadBytes);
    }

    public static RadarProcessingProviderQueueRecentDetail FromProcessingResult(
        RadarProcessingQueuedBatchProcessingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new RadarProcessingProviderQueueRecentDetail(
            RadarProcessingProviderQueueRecentDetailKind.Processing,
            result.Sequence,
            processingStatus: result.Status,
            message: result.Message);
    }

    internal static void EnsureKnownKind(
        RadarProcessingProviderQueueRecentDetailKind kind)
    {
        if (kind is not RadarProcessingProviderQueueRecentDetailKind.Enqueue and
            not RadarProcessingProviderQueueRecentDetailKind.Dequeue and
            not RadarProcessingProviderQueueRecentDetailKind.Processing)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private static void EnsureNonNegative(
        TimeSpan value,
        string paramName)
    {
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }
    }
}
