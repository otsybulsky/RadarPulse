using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Producer-side result from publishing archive batches into an owned queue.
/// </summary>
public sealed class RadarProcessingArchiveQueuedOverlapProducerResult
{
    private RadarProcessingArchiveQueuedOverlapProducerResult(
        RadarProcessingArchiveQueuedOverlapProducerStatus status,
        ArchiveRadarEventBatchPublishResult? publishResult,
        RadarProcessingArchiveQueuedProviderResult? providerResult,
        TimeSpan elapsed,
        string message)
    {
        EnsureKnownStatus(status);
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }

        ArgumentNullException.ThrowIfNull(message);
        if (status == RadarProcessingArchiveQueuedOverlapProducerStatus.Completed)
        {
            ArgumentNullException.ThrowIfNull(publishResult);
        }
        else if (publishResult is not null)
        {
            throw new ArgumentException("Incomplete overlap producer results must not carry an archive publish result.", nameof(publishResult));
        }

        Status = status;
        PublishResult = publishResult;
        ProviderResult = providerResult ?? new RadarProcessingArchiveQueuedProviderResult();
        Elapsed = elapsed;
        Message = message;
    }

    /// <summary>
    /// Producer terminal status.
    /// </summary>
    public RadarProcessingArchiveQueuedOverlapProducerStatus Status { get; }

    /// <summary>
    /// Archive publish result when the producer completed.
    /// </summary>
    public ArchiveRadarEventBatchPublishResult? PublishResult { get; }

    /// <summary>
    /// Queued provider result including retention telemetry.
    /// </summary>
    public RadarProcessingArchiveQueuedProviderResult ProviderResult { get; }

    /// <summary>
    /// Producer elapsed time.
    /// </summary>
    public TimeSpan Elapsed { get; }

    /// <summary>
    /// Producer diagnostic message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Indicates whether the producer completed.
    /// </summary>
    public bool IsCompleted => Status == RadarProcessingArchiveQueuedOverlapProducerStatus.Completed;

    /// <summary>
    /// Indicates whether the producer failed.
    /// </summary>
    public bool IsFailed => Status == RadarProcessingArchiveQueuedOverlapProducerStatus.Failed;

    /// <summary>
    /// Indicates whether the producer was canceled.
    /// </summary>
    public bool IsCanceled => Status == RadarProcessingArchiveQueuedOverlapProducerStatus.Canceled;

    /// <summary>
    /// Creates a completed producer result.
    /// </summary>
    public static RadarProcessingArchiveQueuedOverlapProducerResult Completed(
        ArchiveRadarEventBatchPublishResult publishResult,
        RadarProcessingArchiveQueuedProviderResult providerResult,
        TimeSpan elapsed = default) =>
        new(
            RadarProcessingArchiveQueuedOverlapProducerStatus.Completed,
            publishResult,
            providerResult,
            elapsed,
            string.Empty);

    /// <summary>
    /// Creates a failed producer result.
    /// </summary>
    public static RadarProcessingArchiveQueuedOverlapProducerResult Failed(
        string message,
        RadarProcessingArchiveQueuedProviderResult? providerResult = null,
        TimeSpan elapsed = default) =>
        new(
            RadarProcessingArchiveQueuedOverlapProducerStatus.Failed,
            null,
            providerResult,
            elapsed,
            message);

    /// <summary>
    /// Creates a canceled producer result.
    /// </summary>
    public static RadarProcessingArchiveQueuedOverlapProducerResult Canceled(
        RadarProcessingArchiveQueuedProviderResult? providerResult = null,
        TimeSpan elapsed = default,
        string message = "") =>
        new(
            RadarProcessingArchiveQueuedOverlapProducerStatus.Canceled,
            null,
            providerResult,
            elapsed,
            message);

    internal static void EnsureKnownStatus(
        RadarProcessingArchiveQueuedOverlapProducerStatus status)
    {
        if (status is not RadarProcessingArchiveQueuedOverlapProducerStatus.NotStarted and
            not RadarProcessingArchiveQueuedOverlapProducerStatus.Completed and
            not RadarProcessingArchiveQueuedOverlapProducerStatus.Failed and
            not RadarProcessingArchiveQueuedOverlapProducerStatus.Canceled)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
    }
}
