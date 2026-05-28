using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingSyntheticBenchmark
{
    private static IReadOnlyList<RadarProcessingBenchmarkShardDistribution> CreateShardDistributions(
        RadarProcessingSyntheticWorkload workload,
        RadarProcessingCoreOptions coreOptions)
    {
        var topology = new RadarProcessingTopology(workload.SourceUniverse, coreOptions);
        var shardEventCounts = new long[topology.ShardCount];

        foreach (var batch in workload.Batches)
        {
            foreach (var streamEvent in batch.Events.Span)
            {
                shardEventCounts[topology.GetShardIdForSource(streamEvent.SourceId)]++;
            }
        }

        var result = new RadarProcessingBenchmarkShardDistribution[shardEventCounts.Length];
        for (var shardId = 0; shardId < result.Length; shardId++)
        {
            result[shardId] = new RadarProcessingBenchmarkShardDistribution(
                shardId,
                shardEventCounts[shardId]);
        }

        return Array.AsReadOnly(result);
    }

    private static void EnsureKnownHandlerSet(RadarProcessingBenchmarkHandlerSet handlerSet)
    {
        RadarProcessingBenchmarkHandlers.EnsureKnown(handlerSet);
    }

    private static void EnsureKnownExecutionMode(RadarProcessingExecutionMode executionMode)
    {
        if (executionMode is not RadarProcessingExecutionMode.Sequential and
            not RadarProcessingExecutionMode.PartitionedBarrier and
            not RadarProcessingExecutionMode.AsyncShardTransport)
        {
            throw new ArgumentOutOfRangeException(nameof(executionMode));
        }
    }
}
