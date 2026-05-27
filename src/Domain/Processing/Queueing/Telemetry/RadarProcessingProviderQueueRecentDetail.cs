namespace RadarPulse.Domain.Processing;

/// <summary>
/// Bounded diagnostic detail for recent provider queue activity.
/// </summary>
/// <remarks>
/// Recent details are intentionally compact so telemetry can retain representative
/// enqueue, dequeue, and processing evidence without keeping entire batches alive.
/// </remarks>
public sealed record RadarProcessingProviderQueueRecentDetail
{
    /// <summary>
    /// Creates a recent queue detail with validated counters and timing values.
    /// </summary>
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

    /// <summary>
    /// Detail category.
    /// </summary>
    public RadarProcessingProviderQueueRecentDetailKind Kind { get; }

    /// <summary>
    /// Provider sequence associated with the detail when available.
    /// </summary>
    public RadarProcessingQueuedBatchSequence? Sequence { get; }

    /// <summary>
    /// Enqueue status for enqueue details.
    /// </summary>
    public RadarProcessingQueuedBatchEnqueueStatus? EnqueueStatus { get; }

    /// <summary>
    /// Processing status for processing details.
    /// </summary>
    public RadarProcessingQueuedBatchProcessingStatus? ProcessingStatus { get; }

    /// <summary>
    /// Stream event count associated with the detail.
    /// </summary>
    public int StreamEventCount { get; }

    /// <summary>
    /// Payload bytes associated with the detail.
    /// </summary>
    public long PayloadBytes { get; }

    /// <summary>
    /// Payload value count associated with the detail.
    /// </summary>
    public long PayloadValueCount { get; }

    /// <summary>
    /// Owned snapshot time associated with enqueue or dequeue detail.
    /// </summary>
    public TimeSpan OwnedSnapshotTime { get; }

    /// <summary>
    /// Allocated bytes attributed to owned snapshot creation.
    /// </summary>
    public long OwnedSnapshotAllocatedBytes { get; }

    /// <summary>
    /// Time spent waiting to enqueue.
    /// </summary>
    public TimeSpan EnqueueWaitTime { get; }

    /// <summary>
    /// Time between provider enqueue and processing dequeue.
    /// </summary>
    public TimeSpan ProviderToProcessingLatency { get; }

    /// <summary>
    /// Queue depth observed with the detail.
    /// </summary>
    public int QueueDepth { get; }

    /// <summary>
    /// Retained payload bytes observed with the detail.
    /// </summary>
    public long QueuedPayloadBytes { get; }

    /// <summary>
    /// Optional diagnostic message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Creates a recent detail from an enqueue result.
    /// </summary>
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

    /// <summary>
    /// Creates a recent detail from a dequeued batch.
    /// </summary>
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

    /// <summary>
    /// Creates a recent detail from a processing result.
    /// </summary>
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
