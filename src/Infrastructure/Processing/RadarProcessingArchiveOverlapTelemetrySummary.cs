using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingArchiveOverlapTelemetrySummary
{
    public static RadarProcessingArchiveOverlapTelemetrySummary Empty { get; } = new();

    public RadarProcessingArchiveOverlapTelemetrySummary(
        RadarProcessingRetainedPayloadStrategy retentionStrategy = RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
        TimeSpan elapsed = default,
        TimeSpan producerActiveTime = default,
        TimeSpan consumerActiveTime = default,
        TimeSpan overlapElapsed = default,
        long measuredAllocatedBytes = 0,
        RadarProcessingProviderQueueTelemetrySummary? queueTelemetry = null,
        RadarProcessingRetainedPayloadTelemetrySummary? retentionTelemetry = null)
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
        QueueTelemetry = queueTelemetry ?? RadarProcessingProviderQueueTelemetrySummary.Empty;
        RetentionTelemetry = retentionTelemetry ?? RadarProcessingRetainedPayloadTelemetrySummary.Empty;
    }

    public RadarProcessingRetainedPayloadStrategy RetentionStrategy { get; }

    public TimeSpan Elapsed { get; }

    public TimeSpan ProducerActiveTime { get; }

    public TimeSpan ConsumerActiveTime { get; }

    public TimeSpan OverlapElapsed { get; }

    public long MeasuredAllocatedBytes { get; }

    public RadarProcessingProviderQueueTelemetrySummary QueueTelemetry { get; }

    public RadarProcessingRetainedPayloadTelemetrySummary RetentionTelemetry { get; }

    public bool HasProducerConsumerOverlap => OverlapElapsed > TimeSpan.Zero;

    public bool HasQueuedAheadOverlap => QueueDepthHighWatermark > 1;

    public int QueueDepthHighWatermark => QueueTelemetry.QueueDepthHighWatermark;

    public long RetainedPayloadBytesHighWatermark => QueueTelemetry.RetainedPayloadBytesHighWatermark;

    public TimeSpan ProviderBlockedTime => QueueTelemetry.TotalEnqueueWaitTime;

    public TimeSpan ProducerBlockedTime => ProviderBlockedTime;

    public TimeSpan ConsumerIdleTime => QueueTelemetry.TotalDequeueWaitTime;

    public long RetainedBatchCount => RetentionTelemetry.RetainedBatchCount;

    public long RetainedEventCount => RetentionTelemetry.RetainedEventCount;

    public long RetainedPayloadBytes => RetentionTelemetry.RetainedPayloadBytes;

    public long RetainedPayloadValueCount => RetentionTelemetry.RetainedPayloadValueCount;

    public long RetentionAllocatedBytes => RetentionTelemetry.AllocatedBytes;

    public TimeSpan TotalRetentionTime => RetentionTelemetry.TotalRetentionTime;

    public TimeSpan TotalProviderToProcessingLatency => QueueTelemetry.TotalProviderToProcessingLatency;

    public long ReleaseAttemptCount => RetentionTelemetry.ReleaseAttemptCount;

    public long ReleasedBatchCount => RetentionTelemetry.ReleasedBatchCount;

    public long ReleaseFailedCount => RetentionTelemetry.ReleaseFailedCount;

    public long ReleaseNotRequiredCount => RetentionTelemetry.ReleaseNotRequiredCount;

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
