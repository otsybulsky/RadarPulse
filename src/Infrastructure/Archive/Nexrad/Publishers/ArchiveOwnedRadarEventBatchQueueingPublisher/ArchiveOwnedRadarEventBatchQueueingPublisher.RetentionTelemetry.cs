using System.Diagnostics;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class ArchiveOwnedRadarEventBatchQueueingPublisher : IArchiveRadarEventBatchPublisher, IDisposable
{
    private RadarProcessingRetainedPayloadTelemetrySummary CreateRetentionTelemetrySummary()
    {
        lock (sync)
        {
            return new RadarProcessingRetainedPayloadTelemetrySummary(
                retainedPayloadOptions.Strategy,
                retentionAttemptCount,
                retainedBatchCount,
                retentionUnsupportedStrategyCount,
                retentionFailedCopyCount,
                retentionCanceledCount,
                retentionInvalidInputCount,
                retainedEventCount,
                retainedPayloadBytes,
                retainedPayloadValueCount,
                retainedAllocatedBytes,
                totalRetentionTime,
                transferCount: retainedBatchCount,
                poolRentCount: retainedPoolRentCount,
                poolReturnCount: retainedPoolReturnCount,
                poolMissCount: retainedPoolMissCount,
                releaseAttemptCount: releaseAttemptCount,
                releasedBatchCount: releasedBatchCount,
                alreadyReleasedBatchCount: alreadyReleasedBatchCount,
                releaseFailedCount: releaseFailedCount,
                releaseNotRequiredCount: releaseNotRequiredCount,
                totalReleaseTime: totalReleaseTime,
                eventPoolRentCount: retainedEventPoolRentCount,
                eventPoolReturnCount: retainedEventPoolReturnCount,
                eventPoolMissCount: retainedEventPoolMissCount,
                payloadPoolRentCount: retainedPayloadPoolRentCount,
                payloadPoolReturnCount: retainedPayloadPoolReturnCount,
                payloadPoolMissCount: retainedPayloadPoolMissCount);
        }
    }
}
