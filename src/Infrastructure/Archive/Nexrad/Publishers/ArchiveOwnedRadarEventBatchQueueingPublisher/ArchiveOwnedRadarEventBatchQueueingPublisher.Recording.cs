using System.Diagnostics;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class ArchiveOwnedRadarEventBatchQueueingPublisher : IArchiveRadarEventBatchPublisher, IDisposable
{
    private RadarProcessingQueuedBatchEnqueueResult[] GetEnqueueResultsSnapshot()
    {
        lock (sync)
        {
            return enqueueResults.ToArray();
        }
    }

    private void RecordEnqueueResult(
        RadarProcessingQueuedBatchEnqueueResult result)
    {
        lock (sync)
        {
            enqueueResults.Add(result);
        }
    }

    private void RecordRetentionResult(
        RadarProcessingRetainedPayloadRetentionResult result)
    {
        lock (sync)
        {
            retentionAttemptCount++;
            switch (result.Status)
            {
                case RadarProcessingRetainedPayloadRetentionStatus.Succeeded:
                    retainedBatchCount++;
                    retainedEventCount = checked(retainedEventCount + result.EventCount);
                    retainedPayloadBytes = checked(retainedPayloadBytes + result.PayloadBytes);
                    retainedPayloadValueCount = checked(retainedPayloadValueCount + result.PayloadValueCount);
                    retainedAllocatedBytes = checked(retainedAllocatedBytes + result.AllocatedBytes);
                    retainedPoolRentCount = checked(retainedPoolRentCount + result.PoolRentCount);
                    retainedPoolMissCount = checked(retainedPoolMissCount + result.PoolMissCount);
                    retainedEventPoolRentCount = checked(retainedEventPoolRentCount + result.EventPoolRentCount);
                    retainedEventPoolMissCount = checked(retainedEventPoolMissCount + result.EventPoolMissCount);
                    retainedPayloadPoolRentCount = checked(retainedPayloadPoolRentCount + result.PayloadPoolRentCount);
                    retainedPayloadPoolMissCount = checked(retainedPayloadPoolMissCount + result.PayloadPoolMissCount);
                    totalRetentionTime += result.Elapsed;
                    break;

                case RadarProcessingRetainedPayloadRetentionStatus.UnsupportedStrategy:
                    retentionUnsupportedStrategyCount++;
                    break;

                case RadarProcessingRetainedPayloadRetentionStatus.FailedCopy:
                    retentionFailedCopyCount++;
                    break;

                case RadarProcessingRetainedPayloadRetentionStatus.Canceled:
                    retentionCanceledCount++;
                    break;

                case RadarProcessingRetainedPayloadRetentionStatus.InvalidInput:
                    retentionInvalidInputCount++;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(result));
            }
        }
    }

    private void RecordReleaseResult(
        RadarProcessingRetainedPayloadReleaseResult result)
    {
        lock (sync)
        {
            releaseAttemptCount++;
            totalReleaseTime += result.Elapsed;
            retainedPoolReturnCount = checked(retainedPoolReturnCount + result.PoolReturnCount);
            retainedEventPoolReturnCount = checked(retainedEventPoolReturnCount + result.EventPoolReturnCount);
            retainedPayloadPoolReturnCount = checked(retainedPayloadPoolReturnCount + result.PayloadPoolReturnCount);
            switch (result.Status)
            {
                case RadarProcessingRetainedPayloadReleaseStatus.Released:
                    releasedBatchCount++;
                    break;

                case RadarProcessingRetainedPayloadReleaseStatus.AlreadyReleased:
                    alreadyReleasedBatchCount++;
                    break;

                case RadarProcessingRetainedPayloadReleaseStatus.Failed:
                    releaseFailedCount++;
                    break;

                case RadarProcessingRetainedPayloadReleaseStatus.NotRequired:
                    releaseNotRequiredCount++;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(result));
            }
        }
    }

    private RadarProcessingProviderQueueTelemetrySummary CreateQueueTelemetrySummary() =>
        queue.CreateTelemetrySummary().WithRetainedResourcePressure(
            retainedResourcePressureRecorder.CreateSummary());
}
