namespace RadarPulse.Domain.Processing;

/// <summary>
/// Result of attempting to enqueue one owned provider batch.
/// </summary>
/// <remarks>
/// Accepted results always carry the retained batch. Rejected results never carry
/// a batch, which makes ownership explicit for callers deciding whether to retry,
/// release, or account for retained payload.
/// </remarks>
public sealed record RadarProcessingQueuedBatchEnqueueResult
{
    private RadarProcessingQueuedBatchEnqueueResult(
        RadarProcessingQueuedBatchEnqueueStatus status,
        RadarProcessingQueuedBatch? batch,
        TimeSpan enqueueWaitTime,
        string message)
    {
        EnsureKnownStatus(status);
        if (enqueueWaitTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(enqueueWaitTime));
        }

        ArgumentNullException.ThrowIfNull(message);
        if (status == RadarProcessingQueuedBatchEnqueueStatus.Accepted)
        {
            ArgumentNullException.ThrowIfNull(batch);
        }
        else if (batch is not null)
        {
            throw new ArgumentException("Rejected enqueue results must not carry a queued batch.", nameof(batch));
        }

        Status = status;
        Batch = batch;
        EnqueueWaitTime = enqueueWaitTime;
        Message = message;
    }

    /// <summary>
    /// Enqueue outcome.
    /// </summary>
    public RadarProcessingQueuedBatchEnqueueStatus Status { get; }

    /// <summary>
    /// Retained queued batch when the enqueue was accepted.
    /// </summary>
    public RadarProcessingQueuedBatch? Batch { get; }

    /// <summary>
    /// Provider sequence for accepted batches.
    /// </summary>
    public RadarProcessingQueuedBatchSequence? Sequence => Batch?.Sequence;

    /// <summary>
    /// Time spent waiting for enqueue acceptance or rejection.
    /// </summary>
    public TimeSpan EnqueueWaitTime { get; }

    /// <summary>
    /// Optional diagnostic message for rejected enqueue attempts.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Indicates whether the queue accepted the batch.
    /// </summary>
    public bool IsAccepted => Status == RadarProcessingQueuedBatchEnqueueStatus.Accepted;

    /// <summary>
    /// Creates an accepted enqueue result for a retained batch.
    /// </summary>
    public static RadarProcessingQueuedBatchEnqueueResult Accepted(
        RadarProcessingQueuedBatch batch,
        TimeSpan enqueueWaitTime = default) =>
        new(
            RadarProcessingQueuedBatchEnqueueStatus.Accepted,
            batch,
            enqueueWaitTime,
            string.Empty);

    /// <summary>
    /// Creates a rejected result for a full queue.
    /// </summary>
    public static RadarProcessingQueuedBatchEnqueueResult Full(
        TimeSpan enqueueWaitTime = default,
        string message = "") =>
        Rejected(RadarProcessingQueuedBatchEnqueueStatus.Full, enqueueWaitTime, message);

    /// <summary>
    /// Creates a rejected result for an enqueue timeout.
    /// </summary>
    public static RadarProcessingQueuedBatchEnqueueResult TimedOut(
        TimeSpan enqueueWaitTime,
        string message = "") =>
        Rejected(RadarProcessingQueuedBatchEnqueueStatus.TimedOut, enqueueWaitTime, message);

    /// <summary>
    /// Creates a rejected result for a canceled enqueue attempt.
    /// </summary>
    public static RadarProcessingQueuedBatchEnqueueResult Canceled(
        TimeSpan enqueueWaitTime = default,
        string message = "") =>
        Rejected(RadarProcessingQueuedBatchEnqueueStatus.Canceled, enqueueWaitTime, message);

    /// <summary>
    /// Creates a rejected result for a queue closed to new work.
    /// </summary>
    public static RadarProcessingQueuedBatchEnqueueResult Closed(
        TimeSpan enqueueWaitTime = default,
        string message = "") =>
        Rejected(RadarProcessingQueuedBatchEnqueueStatus.Closed, enqueueWaitTime, message);

    /// <summary>
    /// Creates a rejected result for a faulted queue.
    /// </summary>
    public static RadarProcessingQueuedBatchEnqueueResult Faulted(
        TimeSpan enqueueWaitTime = default,
        string message = "") =>
        Rejected(RadarProcessingQueuedBatchEnqueueStatus.Faulted, enqueueWaitTime, message);

    internal static void EnsureKnownStatus(
        RadarProcessingQueuedBatchEnqueueStatus status)
    {
        if (status is not RadarProcessingQueuedBatchEnqueueStatus.Accepted and
            not RadarProcessingQueuedBatchEnqueueStatus.Full and
            not RadarProcessingQueuedBatchEnqueueStatus.TimedOut and
            not RadarProcessingQueuedBatchEnqueueStatus.Canceled and
            not RadarProcessingQueuedBatchEnqueueStatus.Closed and
            not RadarProcessingQueuedBatchEnqueueStatus.Faulted)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
    }

    private static RadarProcessingQueuedBatchEnqueueResult Rejected(
        RadarProcessingQueuedBatchEnqueueStatus status,
        TimeSpan enqueueWaitTime,
        string message) =>
        new(status, null, enqueueWaitTime, message);
}
