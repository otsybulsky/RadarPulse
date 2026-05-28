using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingBatchRouterTests
{
    private static RadarProcessingRouteMetrics SumPartitionMetrics(RadarProcessingBatchRoute route)
    {
        var metrics = RadarProcessingRouteMetrics.Empty;
        foreach (var partition in route.Partitions)
        {
            metrics = metrics.Add(partition.Metrics);
        }

        return metrics;
    }

    private static RadarProcessingRouteMetrics SumShardMetrics(RadarProcessingBatchRoute route)
    {
        var metrics = RadarProcessingRouteMetrics.Empty;
        foreach (var shard in route.Shards)
        {
            metrics = metrics.Add(shard.Metrics);
        }

        return metrics;
    }

    private static int[] FilterSourceIndexes(
        int[] eventIndexes,
        RadarEventBatch batch,
        int sourceId)
    {
        var result = new List<int>();
        foreach (var eventIndex in eventIndexes)
        {
            if (batch.Events.Span[eventIndex].SourceId == sourceId)
            {
                result.Add(eventIndex);
            }
        }

        return result.ToArray();
    }
}
