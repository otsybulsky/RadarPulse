using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

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

    public RadarProcessingArchiveQueuedOverlapProducerStatus Status { get; }

    public ArchiveRadarEventBatchPublishResult? PublishResult { get; }

    public RadarProcessingArchiveQueuedProviderResult ProviderResult { get; }

    public TimeSpan Elapsed { get; }

    public string Message { get; }

    public bool IsCompleted => Status == RadarProcessingArchiveQueuedOverlapProducerStatus.Completed;

    public bool IsFailed => Status == RadarProcessingArchiveQueuedOverlapProducerStatus.Failed;

    public bool IsCanceled => Status == RadarProcessingArchiveQueuedOverlapProducerStatus.Canceled;

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
