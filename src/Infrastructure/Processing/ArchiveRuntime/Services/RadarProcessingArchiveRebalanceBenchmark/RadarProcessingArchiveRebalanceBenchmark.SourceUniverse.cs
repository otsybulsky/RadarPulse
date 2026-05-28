using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveRebalanceBenchmark
{
    private static RadarSourceUniverse CreateCacheSourceUniverse(
        DirectoryInfo directoryInfo,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        CancellationToken cancellationToken) =>
        RadarProcessingArchiveBenchmarkCacheSelection.CreateSourceUniverse(
            directoryInfo,
            date,
            radarId,
            maxFiles,
            cancellationToken);
    private static DefaultRetainedPayloadPrewarm? CreateDefaultRetainedPayloadPrewarm(
        RadarProcessingArchiveProviderMode providerMode,
        RadarProcessingQueuedProviderOverlapMode providerOverlapMode,
        RadarProcessingRetainedPayloadStrategy retentionStrategy,
        RadarProcessingExecutionMode executionMode,
        RadarProcessingAsyncExecutionOptions? asyncExecution,
        int providerQueueCapacity,
        long? retainedPayloadBytes,
        TimeSpan overlapConsumerDelay,
        RadarProcessingRetainedPayloadFactory? retainedPayloadFactory)
    {
        if (!RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmEnabled ||
            retainedPayloadFactory is not null ||
            !RadarProcessingArchiveRebalanceRolloutDefaults.Matches(
                providerMode,
                providerOverlapMode,
                retentionStrategy,
                executionMode,
                asyncExecution,
                providerQueueCapacity,
                retainedPayloadBytes,
                overlapConsumerDelay))
        {
            return null;
        }

        var factory = new RadarProcessingRetainedPayloadFactory();
        var prewarm = factory.Prewarm(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmEventCount,
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmPayloadBytes,
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmBatchCount);
        return new DefaultRetainedPayloadPrewarm(factory, prewarm);
    }
}
