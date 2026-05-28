namespace RadarPulse.Domain.Processing;

public sealed partial class RadarSourceProcessingStateStore
{
    private void EnsureSourceId(int sourceId)
    {
        if ((uint)sourceId < (uint)SourceCount)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(sourceId));
    }

    private static int FindFirstEventIndex(
        RadarProcessingBatchDelta delta,
        int sourceId)
    {
        var routedEvents = delta.Route.RoutedEvents.Span;
        for (var i = 0; i < routedEvents.Length; i++)
        {
            if (routedEvents[i].SourceId == sourceId)
            {
                return routedEvents[i].EventIndex;
            }
        }

        return -1;
    }

    private RadarProcessingResult CreateInvalidResult(
        RadarProcessingCoreOptions options,
        RadarProcessingTopologyVersion topologyVersion,
        long processedBatchCount,
        RadarProcessingValidationError error,
        int sourceId,
        int eventIndex,
        string message)
    {
        var metrics = CreateMetrics(processedBatchCount);
        return new RadarProcessingResult(
            options.ExecutionMode,
            options.PartitionCount,
            options.ShardCount,
            metrics,
            RadarProcessingValidationResult.Invalid(
                error,
                sourceId,
                eventIndex,
                message,
                metrics),
            topologyVersion: topologyVersion);
    }
}
