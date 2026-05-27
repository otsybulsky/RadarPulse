using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// End-to-end result for archive producer and queued processing consumer overlap.
/// </summary>
public sealed class RadarProcessingArchiveQueuedOverlapResult
{
    /// <summary>
    /// Creates an overlap result with producer, consumer, queue, telemetry, and prewarm evidence.
    /// </summary>
    public RadarProcessingArchiveQueuedOverlapResult(
        RadarProcessingArchiveQueuedOverlapStatus status,
        RadarProcessingArchiveQueuedOverlapProducerResult producer,
        RadarProcessingArchiveQueuedOverlapConsumerResult consumer,
        RadarProcessingProviderQueueTelemetrySummary? queueTelemetry = null,
        RadarProcessingArchiveOverlapTelemetrySummary? overlapTelemetry = null,
        TimeSpan elapsed = default,
        string message = "",
        RadarProcessingRetainedPayloadPrewarmResult? retainedPayloadPrewarm = null)
    {
        EnsureKnownStatus(status);
        ArgumentNullException.ThrowIfNull(producer);
        ArgumentNullException.ThrowIfNull(consumer);
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }

        ArgumentNullException.ThrowIfNull(message);

        Status = status;
        Producer = producer;
        Consumer = consumer;
        QueueTelemetry = queueTelemetry ?? RadarProcessingProviderQueueTelemetrySummary.Empty;
        OverlapTelemetry = overlapTelemetry ?? RadarProcessingArchiveOverlapTelemetrySummary.Empty;
        Elapsed = elapsed;
        Message = message;
        RetainedPayloadPrewarm = retainedPayloadPrewarm ?? RadarProcessingRetainedPayloadPrewarmResult.None;
    }

    /// <summary>
    /// Terminal overlap status.
    /// </summary>
    public RadarProcessingArchiveQueuedOverlapStatus Status { get; }

    /// <summary>
    /// Producer-side result.
    /// </summary>
    public RadarProcessingArchiveQueuedOverlapProducerResult Producer { get; }

    /// <summary>
    /// Consumer-side result.
    /// </summary>
    public RadarProcessingArchiveQueuedOverlapConsumerResult Consumer { get; }

    /// <summary>
    /// Provider queue telemetry.
    /// </summary>
    public RadarProcessingProviderQueueTelemetrySummary QueueTelemetry { get; }

    /// <summary>
    /// Combined producer/consumer overlap telemetry.
    /// </summary>
    public RadarProcessingArchiveOverlapTelemetrySummary OverlapTelemetry { get; }

    /// <summary>
    /// Alias for overlap telemetry used by read-model mappers.
    /// </summary>
    public RadarProcessingArchiveOverlapTelemetrySummary Telemetry => OverlapTelemetry;

    /// <summary>
    /// End-to-end elapsed time.
    /// </summary>
    public TimeSpan Elapsed { get; }

    /// <summary>
    /// Terminal overlap message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Retained payload pool prewarm evidence.
    /// </summary>
    public RadarProcessingRetainedPayloadPrewarmResult RetainedPayloadPrewarm { get; }

    /// <summary>
    /// Indicates whether retained payload prewarm was applied.
    /// </summary>
    public bool HasRetainedPayloadPrewarm => RetainedPayloadPrewarm.Applied;

    /// <summary>
    /// Producer provider result.
    /// </summary>
    public RadarProcessingArchiveQueuedProviderResult ProviderResult => Producer.ProviderResult;

    /// <summary>
    /// Retained resource pressure captured by queue telemetry.
    /// </summary>
    public RadarProcessingRetainedResourcePressureSummary RetainedResourcePressure =>
        QueueTelemetry.RetainedResourcePressure;

    /// <summary>
    /// Pending retained payload byte high watermark.
    /// </summary>
    public long PendingRetainedPayloadBytesHighWatermark =>
        RetainedResourcePressure.PendingRetainedPayloadBytesHighWatermark;

    /// <summary>
    /// Active retained payload byte high watermark.
    /// </summary>
    public long ActiveRetainedPayloadBytesHighWatermark =>
        RetainedResourcePressure.ActiveRetainedPayloadBytesHighWatermark;

    /// <summary>
    /// Combined retained payload byte high watermark.
    /// </summary>
    public long CombinedRetainedPayloadBytesHighWatermark =>
        RetainedResourcePressure.CombinedRetainedPayloadBytesHighWatermark;

    /// <summary>
    /// Indicates whether producer and consumer completed.
    /// </summary>
    public bool IsCompleted => Status == RadarProcessingArchiveQueuedOverlapStatus.Completed;

    /// <summary>
    /// Indicates whether producer or consumer faulted.
    /// </summary>
    public bool IsFaulted =>
        Status is RadarProcessingArchiveQueuedOverlapStatus.ProducerFailed or
            RadarProcessingArchiveQueuedOverlapStatus.ConsumerFaulted;

    /// <summary>
    /// Indicates whether the overlap run was canceled.
    /// </summary>
    public bool IsCanceled => Status == RadarProcessingArchiveQueuedOverlapStatus.Canceled;

    internal static void EnsureKnownStatus(
        RadarProcessingArchiveQueuedOverlapStatus status)
    {
        if (status is not RadarProcessingArchiveQueuedOverlapStatus.NotStarted and
            not RadarProcessingArchiveQueuedOverlapStatus.Completed and
            not RadarProcessingArchiveQueuedOverlapStatus.ProducerFailed and
            not RadarProcessingArchiveQueuedOverlapStatus.ConsumerFaulted and
            not RadarProcessingArchiveQueuedOverlapStatus.Canceled and
            not RadarProcessingArchiveQueuedOverlapStatus.Disposed)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
    }
}
