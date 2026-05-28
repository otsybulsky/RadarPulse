namespace RadarPulse.Domain.Processing;

public static partial class RadarProcessingAsyncValidator
{
    private static RadarProcessingRecentWorkerBatch? GetLatestWorkerBatch(
        RadarProcessingWorkerTelemetrySummary? workerTelemetry) =>
        workerTelemetry?.RecentBatches.Count > 0
            ? workerTelemetry.RecentBatches[^1]
            : null;

    private static RadarProcessingRouteMetrics SumPartitionMetrics(
        RadarProcessingTelemetry telemetry)
    {
        var metrics = RadarProcessingRouteMetrics.Empty;
        foreach (var partition in telemetry.Partitions)
        {
            metrics = metrics.Add(partition.Metrics);
        }

        return metrics;
    }

    private static RadarProcessingRouteMetrics SumShardMetrics(
        RadarProcessingTelemetry telemetry)
    {
        var metrics = RadarProcessingRouteMetrics.Empty;
        foreach (var shard in telemetry.Shards)
        {
            metrics = metrics.Add(shard.Metrics);
        }

        return metrics;
    }

    private static RadarProcessingAsyncValidationResult Invalid(
        RadarProcessingAsyncValidationError error,
        string message,
        RadarProcessingValidationProfile validationProfile,
        ulong? synchronousChecksum = null,
        ulong? asyncChecksum = null) =>
        RadarProcessingAsyncValidationResult.Invalid(
            error,
            message,
            validationProfile,
            synchronousChecksum,
            asyncChecksum);
}
