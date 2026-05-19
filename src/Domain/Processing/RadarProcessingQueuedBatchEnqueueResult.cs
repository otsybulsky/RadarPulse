namespace RadarPulse.Domain.Processing;

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

    public RadarProcessingQueuedBatchEnqueueStatus Status { get; }

    public RadarProcessingQueuedBatch? Batch { get; }

    public RadarProcessingQueuedBatchSequence? Sequence => Batch?.Sequence;

    public TimeSpan EnqueueWaitTime { get; }

    public string Message { get; }

    public bool IsAccepted => Status == RadarProcessingQueuedBatchEnqueueStatus.Accepted;

    public static RadarProcessingQueuedBatchEnqueueResult Accepted(
        RadarProcessingQueuedBatch batch,
        TimeSpan enqueueWaitTime = default) =>
        new(
            RadarProcessingQueuedBatchEnqueueStatus.Accepted,
            batch,
            enqueueWaitTime,
            string.Empty);

    public static RadarProcessingQueuedBatchEnqueueResult Full(
        TimeSpan enqueueWaitTime = default,
        string message = "") =>
        Rejected(RadarProcessingQueuedBatchEnqueueStatus.Full, enqueueWaitTime, message);

    public static RadarProcessingQueuedBatchEnqueueResult TimedOut(
        TimeSpan enqueueWaitTime,
        string message = "") =>
        Rejected(RadarProcessingQueuedBatchEnqueueStatus.TimedOut, enqueueWaitTime, message);

    public static RadarProcessingQueuedBatchEnqueueResult Canceled(
        TimeSpan enqueueWaitTime = default,
        string message = "") =>
        Rejected(RadarProcessingQueuedBatchEnqueueStatus.Canceled, enqueueWaitTime, message);

    public static RadarProcessingQueuedBatchEnqueueResult Closed(
        TimeSpan enqueueWaitTime = default,
        string message = "") =>
        Rejected(RadarProcessingQueuedBatchEnqueueStatus.Closed, enqueueWaitTime, message);

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
