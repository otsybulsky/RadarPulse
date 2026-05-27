using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Telemetry summary for archive producer and processing consumer overlap.
/// </summary>
public sealed record RadarProcessingArchiveOverlapTelemetrySummary
{
    /// <summary>
    /// Empty overlap telemetry summary.
    /// </summary>
    public static RadarProcessingArchiveOverlapTelemetrySummary Empty { get; } = new();

    /// <summary>
    /// Creates overlap telemetry from queue, retention, timing, and allocation evidence.
    /// </summary>
    public RadarProcessingArchiveOverlapTelemetrySummary(
        RadarProcessingRetainedPayloadStrategy retentionStrategy = RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
        TimeSpan elapsed = default,
        TimeSpan producerActiveTime = default,
        TimeSpan consumerActiveTime = default,
        TimeSpan overlapElapsed = default,
        long measuredAllocatedBytes = 0,
        RadarProcessingProviderQueueTelemetrySummary? queueTelemetry = null,
        RadarProcessingRetainedPayloadTelemetrySummary? retentionTelemetry = null,
        RadarProcessingRetainedResourcePressureSummary? retainedResourcePressure = null)
    {
        RadarProcessingRetainedPayloadOptions.EnsureKnownStrategy(retentionStrategy);
        EnsureNonNegative(elapsed, nameof(elapsed));
        EnsureNonNegative(producerActiveTime, nameof(producerActiveTime));
        EnsureNonNegative(consumerActiveTime, nameof(consumerActiveTime));
        EnsureNonNegative(overlapElapsed, nameof(overlapElapsed));
        ArgumentOutOfRangeException.ThrowIfNegative(measuredAllocatedBytes);

        if (overlapElapsed > producerActiveTime ||
            overlapElapsed > consumerActiveTime ||
            overlapElapsed > elapsed)
        {
            throw new ArgumentOutOfRangeException(
                nameof(overlapElapsed),
                overlapElapsed,
                "Overlap elapsed cannot exceed elapsed, producer active, or consumer active time.");
        }

        RetentionStrategy = retentionStrategy;
        Elapsed = elapsed;
        ProducerActiveTime = producerActiveTime;
        ConsumerActiveTime = consumerActiveTime;
        OverlapElapsed = overlapElapsed;
        MeasuredAllocatedBytes = measuredAllocatedBytes;
        var effectiveQueueTelemetry = queueTelemetry ?? RadarProcessingProviderQueueTelemetrySummary.Empty;
        QueueTelemetry = retainedResourcePressure is null
            ? effectiveQueueTelemetry
            : effectiveQueueTelemetry.WithRetainedResourcePressure(retainedResourcePressure);
        RetentionTelemetry = retentionTelemetry ?? RadarProcessingRetainedPayloadTelemetrySummary.Empty;
    }

    /// <summary>
    /// Retained payload strategy used by the producer.
    /// </summary>
    public RadarProcessingRetainedPayloadStrategy RetentionStrategy { get; }

    /// <summary>
    /// End-to-end elapsed time.
    /// </summary>
    public TimeSpan Elapsed { get; }

    /// <summary>
    /// Time spent by the producer.
    /// </summary>
    public TimeSpan ProducerActiveTime { get; }

    /// <summary>
    /// Time spent by the consumer.
    /// </summary>
    public TimeSpan ConsumerActiveTime { get; }

    /// <summary>
    /// Time window where producer and consumer were both active.
    /// </summary>
    public TimeSpan OverlapElapsed { get; }

    /// <summary>
    /// Measured allocation delta for the overlap run.
    /// </summary>
    public long MeasuredAllocatedBytes { get; }

    /// <summary>
    /// Provider queue telemetry captured for the run.
    /// </summary>
    public RadarProcessingProviderQueueTelemetrySummary QueueTelemetry { get; }

    /// <summary>
    /// Retained payload telemetry captured for the run.
    /// </summary>
    public RadarProcessingRetainedPayloadTelemetrySummary RetentionTelemetry { get; }

    /// <summary>
    /// Indicates whether producer and consumer active windows overlapped.
    /// </summary>
    public bool HasProducerConsumerOverlap => OverlapElapsed > TimeSpan.Zero;

    /// <summary>
    /// Indicates whether queue depth showed producer queue-ahead overlap.
    /// </summary>
    public bool HasQueuedAheadOverlap => QueueDepthHighWatermark > 1;

    /// <summary>
    /// Highest queue depth observed.
    /// </summary>
    public int QueueDepthHighWatermark => QueueTelemetry.QueueDepthHighWatermark;

    /// <summary>
    /// Highest retained payload byte count observed in the provider queue.
    /// </summary>
    public long RetainedPayloadBytesHighWatermark => QueueTelemetry.RetainedPayloadBytesHighWatermark;

    /// <summary>
    /// Combined retained-resource pressure evidence.
    /// </summary>
    public RadarProcessingRetainedResourcePressureSummary RetainedResourcePressure =>
        QueueTelemetry.RetainedResourcePressure;

    /// <summary>
    /// Current pending retained batch count at summary capture.
    /// </summary>
    public long CurrentPendingRetainedBatchCount =>
        RetainedResourcePressure.CurrentPendingRetainedBatchCount;

    /// <summary>
    /// Current pending retained payload bytes at summary capture.
    /// </summary>
    public long CurrentPendingRetainedPayloadBytes =>
        RetainedResourcePressure.CurrentPendingRetainedPayloadBytes;

    /// <summary>
    /// Pending retained batch high watermark.
    /// </summary>
    public long PendingRetainedBatchCountHighWatermark =>
        RetainedResourcePressure.PendingRetainedBatchCountHighWatermark;

    /// <summary>
    /// Pending retained payload byte high watermark.
    /// </summary>
    public long PendingRetainedPayloadBytesHighWatermark =>
        RetainedResourcePressure.PendingRetainedPayloadBytesHighWatermark;

    /// <summary>
    /// Current active retained batch count at summary capture.
    /// </summary>
    public long CurrentActiveRetainedBatchCount =>
        RetainedResourcePressure.CurrentActiveRetainedBatchCount;

    /// <summary>
    /// Current active retained payload bytes at summary capture.
    /// </summary>
    public long CurrentActiveRetainedPayloadBytes =>
        RetainedResourcePressure.CurrentActiveRetainedPayloadBytes;

    /// <summary>
    /// Active retained batch high watermark.
    /// </summary>
    public long ActiveRetainedBatchCountHighWatermark =>
        RetainedResourcePressure.ActiveRetainedBatchCountHighWatermark;

    /// <summary>
    /// Active retained payload byte high watermark.
    /// </summary>
    public long ActiveRetainedPayloadBytesHighWatermark =>
        RetainedResourcePressure.ActiveRetainedPayloadBytesHighWatermark;

    /// <summary>
    /// Current combined retained batch count.
    /// </summary>
    public long CurrentCombinedRetainedBatchCount =>
        RetainedResourcePressure.CurrentCombinedRetainedBatchCount;

    /// <summary>
    /// Current combined retained payload bytes.
    /// </summary>
    public long CurrentCombinedRetainedPayloadBytes =>
        RetainedResourcePressure.CurrentCombinedRetainedPayloadBytes;

    /// <summary>
    /// Combined retained batch high watermark.
    /// </summary>
    public long CombinedRetainedBatchCountHighWatermark =>
        RetainedResourcePressure.CombinedRetainedBatchCountHighWatermark;

    /// <summary>
    /// Combined retained payload byte high watermark.
    /// </summary>
    public long CombinedRetainedPayloadBytesHighWatermark =>
        RetainedResourcePressure.CombinedRetainedPayloadBytesHighWatermark;

    /// <summary>
    /// Time the provider spent blocked on enqueue.
    /// </summary>
    public TimeSpan ProviderBlockedTime => QueueTelemetry.TotalEnqueueWaitTime;

    /// <summary>
    /// Producer blocked time, currently equivalent to provider blocked time.
    /// </summary>
    public TimeSpan ProducerBlockedTime => ProviderBlockedTime;

    /// <summary>
    /// Time the consumer spent waiting for queue input.
    /// </summary>
    public TimeSpan ConsumerIdleTime => QueueTelemetry.TotalDequeueWaitTime;

    /// <summary>
    /// Number of retained batches.
    /// </summary>
    public long RetainedBatchCount => RetentionTelemetry.RetainedBatchCount;

    /// <summary>
    /// Number of retained events.
    /// </summary>
    public long RetainedEventCount => RetentionTelemetry.RetainedEventCount;

    /// <summary>
    /// Retained payload bytes.
    /// </summary>
    public long RetainedPayloadBytes => RetentionTelemetry.RetainedPayloadBytes;

    /// <summary>
    /// Retained payload value count.
    /// </summary>
    public long RetainedPayloadValueCount => RetentionTelemetry.RetainedPayloadValueCount;

    /// <summary>
    /// Allocation bytes attributed to retention.
    /// </summary>
    public long RetentionAllocatedBytes => RetentionTelemetry.AllocatedBytes;

    /// <summary>
    /// Total time spent retaining payloads.
    /// </summary>
    public TimeSpan TotalRetentionTime => RetentionTelemetry.TotalRetentionTime;

    /// <summary>
    /// Total latency from provider enqueue to processing dequeue.
    /// </summary>
    public TimeSpan TotalProviderToProcessingLatency => QueueTelemetry.TotalProviderToProcessingLatency;

    /// <summary>
    /// Number of retained resource release attempts.
    /// </summary>
    public long ReleaseAttemptCount => RetentionTelemetry.ReleaseAttemptCount;

    /// <summary>
    /// Number of retained batches released.
    /// </summary>
    public long ReleasedBatchCount => RetentionTelemetry.ReleasedBatchCount;

    /// <summary>
    /// Number of retained resource release failures.
    /// </summary>
    public long ReleaseFailedCount => RetentionTelemetry.ReleaseFailedCount;

    /// <summary>
    /// Number of retained batches that did not require release.
    /// </summary>
    public long ReleaseNotRequiredCount => RetentionTelemetry.ReleaseNotRequiredCount;

    /// <summary>
    /// Measured allocated bytes not attributed to retained payload copies.
    /// </summary>
    public long UnattributedAllocatedBytes =>
        MeasuredAllocatedBytes > RetentionAllocatedBytes
            ? MeasuredAllocatedBytes - RetentionAllocatedBytes
            : 0;

    internal static RadarProcessingArchiveOverlapTelemetrySummary FromOverlapResult(
        RadarProcessingArchiveQueuedOverlapProducerResult producer,
        RadarProcessingArchiveQueuedOverlapConsumerResult consumer,
        RadarProcessingProviderQueueTelemetrySummary queueTelemetry,
        TimeSpan elapsed,
        long measuredAllocatedBytes)
    {
        ArgumentNullException.ThrowIfNull(producer);
        ArgumentNullException.ThrowIfNull(consumer);
        ArgumentNullException.ThrowIfNull(queueTelemetry);

        var overlapElapsed = CalculateOverlapElapsed(producer.Elapsed, consumer.Elapsed, elapsed);
        var retentionTelemetry = producer.ProviderResult.RetentionTelemetry;
        if (ReferenceEquals(retentionTelemetry, RadarProcessingRetainedPayloadTelemetrySummary.Empty))
        {
            retentionTelemetry = CreateSnapshotRetentionTelemetry(queueTelemetry);
        }

        return new RadarProcessingArchiveOverlapTelemetrySummary(
            retentionTelemetry.Strategy,
            elapsed,
            producer.Elapsed,
            consumer.Elapsed,
            overlapElapsed,
            measuredAllocatedBytes,
            queueTelemetry,
            retentionTelemetry);
    }

    private static RadarProcessingRetainedPayloadTelemetrySummary CreateSnapshotRetentionTelemetry(
        RadarProcessingProviderQueueTelemetrySummary queueTelemetry) =>
        new(
            RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
            retentionAttemptCount: queueTelemetry.EnqueueAttemptCount,
            retainedBatchCount: queueTelemetry.OwnedSnapshotCount,
            retainedEventCount: queueTelemetry.OwnedSnapshotEventCount,
            retainedPayloadBytes: queueTelemetry.OwnedSnapshotPayloadBytes,
            retainedPayloadValueCount: queueTelemetry.OwnedSnapshotPayloadValueCount,
            allocatedBytes: queueTelemetry.OwnedSnapshotAllocatedBytes,
            totalRetentionTime: queueTelemetry.TotalOwnedSnapshotTime,
            releaseAttemptCount: queueTelemetry.OwnedSnapshotCount,
            releaseNotRequiredCount: queueTelemetry.OwnedSnapshotCount);

    private static TimeSpan CalculateOverlapElapsed(
        TimeSpan producerActiveTime,
        TimeSpan consumerActiveTime,
        TimeSpan elapsed)
    {
        var overlapTicks = Math.Min(
            elapsed.Ticks,
            Math.Min(producerActiveTime.Ticks, consumerActiveTime.Ticks));
        return TimeSpan.FromTicks(overlapTicks);
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
