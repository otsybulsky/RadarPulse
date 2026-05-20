using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingArchiveQueuedOverlapResult
{
    public RadarProcessingArchiveQueuedOverlapResult(
        RadarProcessingArchiveQueuedOverlapStatus status,
        RadarProcessingArchiveQueuedOverlapProducerResult producer,
        RadarProcessingArchiveQueuedOverlapConsumerResult consumer,
        RadarProcessingProviderQueueTelemetrySummary? queueTelemetry = null,
        RadarProcessingArchiveOverlapTelemetrySummary? overlapTelemetry = null,
        TimeSpan elapsed = default,
        string message = "")
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
    }

    public RadarProcessingArchiveQueuedOverlapStatus Status { get; }

    public RadarProcessingArchiveQueuedOverlapProducerResult Producer { get; }

    public RadarProcessingArchiveQueuedOverlapConsumerResult Consumer { get; }

    public RadarProcessingProviderQueueTelemetrySummary QueueTelemetry { get; }

    public RadarProcessingArchiveOverlapTelemetrySummary OverlapTelemetry { get; }

    public RadarProcessingArchiveOverlapTelemetrySummary Telemetry => OverlapTelemetry;

    public TimeSpan Elapsed { get; }

    public string Message { get; }

    public RadarProcessingArchiveQueuedProviderResult ProviderResult => Producer.ProviderResult;

    public RadarProcessingRetainedResourcePressureSummary RetainedResourcePressure =>
        QueueTelemetry.RetainedResourcePressure;

    public long PendingRetainedPayloadBytesHighWatermark =>
        RetainedResourcePressure.PendingRetainedPayloadBytesHighWatermark;

    public long ActiveRetainedPayloadBytesHighWatermark =>
        RetainedResourcePressure.ActiveRetainedPayloadBytesHighWatermark;

    public long CombinedRetainedPayloadBytesHighWatermark =>
        RetainedResourcePressure.CombinedRetainedPayloadBytesHighWatermark;

    public bool IsCompleted => Status == RadarProcessingArchiveQueuedOverlapStatus.Completed;

    public bool IsFaulted =>
        Status is RadarProcessingArchiveQueuedOverlapStatus.ProducerFailed or
            RadarProcessingArchiveQueuedOverlapStatus.ConsumerFaulted;

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
