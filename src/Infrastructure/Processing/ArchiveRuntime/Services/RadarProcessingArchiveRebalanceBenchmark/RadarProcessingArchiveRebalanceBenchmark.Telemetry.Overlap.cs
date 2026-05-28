namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveRebalanceBenchmark
{
    private static RadarProcessingArchiveOverlapTelemetrySummary AddOverlapTelemetry(
        RadarProcessingArchiveOverlapTelemetrySummary current,
        RadarProcessingArchiveOverlapTelemetrySummary next)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(next);

        var hasCurrent = current.Elapsed > TimeSpan.Zero ||
            current.ProducerActiveTime > TimeSpan.Zero ||
            current.ConsumerActiveTime > TimeSpan.Zero ||
            current.OverlapElapsed > TimeSpan.Zero;
        var hasNext = next.Elapsed > TimeSpan.Zero ||
            next.ProducerActiveTime > TimeSpan.Zero ||
            next.ConsumerActiveTime > TimeSpan.Zero ||
            next.OverlapElapsed > TimeSpan.Zero;
        var strategy = hasNext || !hasCurrent
            ? next.RetentionStrategy
            : current.RetentionStrategy;
        if (hasCurrent &&
            hasNext &&
            current.RetentionStrategy != next.RetentionStrategy)
        {
            throw new InvalidOperationException("Cannot aggregate overlap telemetry from different retention strategies.");
        }

        return new RadarProcessingArchiveOverlapTelemetrySummary(
            strategy,
            current.Elapsed + next.Elapsed,
            current.ProducerActiveTime + next.ProducerActiveTime,
            current.ConsumerActiveTime + next.ConsumerActiveTime,
            current.OverlapElapsed + next.OverlapElapsed,
            checked(current.MeasuredAllocatedBytes + next.MeasuredAllocatedBytes),
            AddQueueTelemetry(current.QueueTelemetry, next.QueueTelemetry),
            AddRetentionTelemetry(current.RetentionTelemetry, next.RetentionTelemetry));
    }
}
