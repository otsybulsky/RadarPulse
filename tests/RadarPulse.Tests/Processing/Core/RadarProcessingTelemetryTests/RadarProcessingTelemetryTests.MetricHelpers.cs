using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingTelemetryTests
{
    private static RadarProcessingRouteMetrics SumPartitionMetrics(RadarProcessingTelemetry telemetry)
    {
        var metrics = RadarProcessingRouteMetrics.Empty;
        foreach (var partition in telemetry.Partitions)
        {
            metrics = metrics.Add(partition.Metrics);
        }

        return metrics;
    }

    private static RadarProcessingRouteMetrics SumShardMetrics(RadarProcessingTelemetry telemetry)
    {
        var metrics = RadarProcessingRouteMetrics.Empty;
        foreach (var shard in telemetry.Shards)
        {
            metrics = metrics.Add(shard.Metrics);
        }

        return metrics;
    }
}
