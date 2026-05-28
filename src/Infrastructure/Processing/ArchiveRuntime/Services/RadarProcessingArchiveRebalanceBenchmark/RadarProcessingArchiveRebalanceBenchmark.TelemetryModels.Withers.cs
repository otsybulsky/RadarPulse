using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveRebalanceBenchmark
{
    private readonly partial record struct ArchiveIterationTelemetry
    {
        public ArchiveIterationTelemetry WithPublishResult(
            RadarPulse.Domain.Archive.ArchiveRadarEventBatchPublishResult result,
            TimeSpan processingElapsed,
            long processingCallbackAllocatedBytes) =>
            this with
            {
                ExaminedFileCount = 1,
                SkippedFileCount = 0,
                PublishedFileCount = result.BatchCount > 0 ? 1 : 0,
                FileSizeBytes = result.FileSizeBytes,
                CompressedRecordCount = result.CompressedRecordCount,
                CompressedBytes = result.CompressedBytes,
                DecompressedBytes = result.DecompressedBytes,
                BatchCount = result.BatchCount,
                EventCount = result.EventCount,
                PayloadBytes = result.PayloadBytes,
                PayloadValueCount = result.PayloadValueCount,
                RawValueChecksum = result.RawValueChecksum,
                ProcessingElapsed = processingElapsed,
                ProcessingCallbackAllocatedBytes = processingCallbackAllocatedBytes
            };

        public ArchiveIterationTelemetry WithQueueTelemetry(
            RadarProcessingProviderQueueTelemetrySummary queueTelemetry)
        {
            ArgumentNullException.ThrowIfNull(queueTelemetry);

            return this with
            {
                QueueTelemetry = queueTelemetry
            };
        }

        public ArchiveIterationTelemetry WithRetentionTelemetry(
            RadarProcessingRetainedPayloadTelemetrySummary retentionTelemetry)
        {
            ArgumentNullException.ThrowIfNull(retentionTelemetry);

            return this with
            {
                RetentionTelemetry = retentionTelemetry
            };
        }

        public ArchiveIterationTelemetry WithOverlapTelemetry(
            RadarProcessingArchiveOverlapTelemetrySummary overlapTelemetry)
        {
            ArgumentNullException.ThrowIfNull(overlapTelemetry);

            return this with
            {
                OverlapTelemetry = overlapTelemetry
            };
        }

        public ArchiveIterationTelemetry WithRetentionStats(
            RadarProcessingRebalanceRetentionStats retentionStats)
        {
            ArgumentNullException.ThrowIfNull(retentionStats);

            return this with
            {
                RetentionStats = retentionStats
            };
        }

        public ArchiveIterationTelemetry WithPublishTotals(
            CacheIterationTotals totals,
            TimeSpan processingElapsed,
            long processingCallbackAllocatedBytes) =>
            this with
            {
                ExaminedFileCount = totals.ExaminedFileCount,
                SkippedFileCount = totals.SkippedFileCount,
                PublishedFileCount = totals.PublishedFileCount,
                FileSizeBytes = totals.FileSizeBytes,
                CompressedRecordCount = totals.CompressedRecordCount,
                CompressedBytes = totals.CompressedBytes,
                DecompressedBytes = totals.DecompressedBytes,
                BatchCount = totals.BatchCount,
                EventCount = totals.EventCount,
                PayloadBytes = totals.PayloadBytes,
                PayloadValueCount = totals.PayloadValueCount,
                RawValueChecksum = totals.RawValueChecksum,
                ProcessingElapsed = processingElapsed,
                ProcessingCallbackAllocatedBytes = processingCallbackAllocatedBytes
            };

        public ArchiveIterationTelemetry WithMetrics(
            RadarProcessingMetrics metrics,
            long topologyVersionCount)
        {
            var validationChecksum = ComputeChecksum(
                metrics,
                topologyVersionCount,
                RebalanceEvaluationCount,
                AcceptedMoveCount,
                SkippedDecisionCount,
                DirectHotReliefCount,
                ColdEvacuationCount,
                FailedMigrationCount,
                ValidationSucceeded);

            return this with
            {
                TopologyVersionCount = topologyVersionCount,
                ValidationChecksum = validationChecksum
            };
        }
    }
}
