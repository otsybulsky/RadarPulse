using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveRebalanceBenchmark
{
    private readonly partial record struct ArchiveIterationTelemetry(
        long ExaminedFileCount,
        long SkippedFileCount,
        long PublishedFileCount,
        long FileSizeBytes,
        long CompressedRecordCount,
        long CompressedBytes,
        long DecompressedBytes,
        long BatchCount,
        long EventCount,
        long PayloadBytes,
        long PayloadValueCount,
        long RawValueChecksum,
        long TopologyVersionCount,
        long RebalanceEvaluationCount,
        long AcceptedMoveCount,
        long SkippedDecisionCount,
        long DirectHotReliefCount,
        long ColdEvacuationCount,
        long FailedMigrationCount,
        bool ValidationSucceeded,
        ulong ValidationChecksum,
        List<RadarProcessingRebalanceSkippedReason>? SkippedReasons,
        List<RadarProcessingRebalanceSkippedReasonCounter>? SkippedReasonCounters,
        List<RadarProcessingSyntheticRebalanceMovePressure>? AcceptedMovePressures,
        RadarProcessingRebalanceRetentionStats RetentionStats,
        TimeSpan ProcessingElapsed,
        long ProcessingCallbackAllocatedBytes,
        RadarProcessingProviderQueueTelemetrySummary QueueTelemetry,
        RadarProcessingRetainedPayloadTelemetrySummary RetentionTelemetry,
        RadarProcessingArchiveOverlapTelemetrySummary OverlapTelemetry,
        long ProcessingValidationFailedBatchCount)
    {
        public static ArchiveIterationTelemetry Empty =>
            new(
                ExaminedFileCount: 0,
                SkippedFileCount: 0,
                PublishedFileCount: 0,
                FileSizeBytes: 0,
                CompressedRecordCount: 0,
                CompressedBytes: 0,
                DecompressedBytes: 0,
                BatchCount: 0,
                EventCount: 0,
                PayloadBytes: 0,
                PayloadValueCount: 0,
                RawValueChecksum: 0,
                TopologyVersionCount: 1,
                RebalanceEvaluationCount: 0,
                AcceptedMoveCount: 0,
                SkippedDecisionCount: 0,
                DirectHotReliefCount: 0,
                ColdEvacuationCount: 0,
                FailedMigrationCount: 0,
                ValidationSucceeded: true,
                ValidationChecksum: ChecksumInitial,
                SkippedReasons: null,
                SkippedReasonCounters: null,
                AcceptedMovePressures: null,
                RetentionStats: new RadarProcessingRebalanceRetentionStats(),
                ProcessingElapsed: TimeSpan.Zero,
                ProcessingCallbackAllocatedBytes: 0,
                QueueTelemetry: RadarProcessingProviderQueueTelemetrySummary.Empty,
                RetentionTelemetry: RadarProcessingRetainedPayloadTelemetrySummary.Empty,
                OverlapTelemetry: RadarProcessingArchiveOverlapTelemetrySummary.Empty,
                ProcessingValidationFailedBatchCount: 0);

        public static ArchiveIterationTelemetry FromMetrics(
            RadarProcessingMetrics metrics,
            long topologyVersionCount,
            long rebalanceEvaluationCount = 0) =>
            Empty.WithMetrics(metrics, topologyVersionCount) with
            {
                RebalanceEvaluationCount = rebalanceEvaluationCount,
                ValidationChecksum = ComputeChecksum(
                    metrics,
                    topologyVersionCount,
                    rebalanceEvaluationCount,
                    acceptedMoveCount: 0,
                    skippedDecisionCount: 0,
                    directHotReliefCount: 0,
                    coldEvacuationCount: 0,
                    failedMigrationCount: 0,
                    validationSucceeded: true)
            };

        public static ArchiveIterationTelemetry FromRebalanceSessionResult(
            RadarProcessingRebalanceSessionResult result)
        {
            List<RadarProcessingRebalanceSkippedReason>? skippedReasons = null;
            List<RadarProcessingRebalanceSkippedReasonCounter>? skippedReasonCounters = null;
            List<RadarProcessingSyntheticRebalanceMovePressure>? movePressures = null;
            var skippedDecisionCount = 0L;
            var acceptedMoveCount = 0L;
            var directHotReliefCount = 0L;
            var coldEvacuationCount = 0L;
            var failedMigrationCount = 0L;

            AddDecision(
                result.DirectHotReliefDecision,
                ref skippedReasons,
                ref skippedReasonCounters,
                ref skippedDecisionCount);
            AddDecision(
                result.ColdEvacuationDecision,
                ref skippedReasons,
                ref skippedReasonCounters,
                ref skippedDecisionCount);

            if (result.PublishedMigration)
            {
                acceptedMoveCount = 1;
                var decision = result.RebalanceDecision ??
                               throw new InvalidDataException("Published moves require a rebalance decision.");
                movePressures = new List<RadarProcessingSyntheticRebalanceMovePressure>(capacity: 1);
                movePressures.Add(CreateMovePressure(decision));
                if (decision.MoveKind == RadarProcessingRebalanceMoveKind.DirectHotRelief)
                {
                    directHotReliefCount = 1;
                }
                else if (decision.MoveKind == RadarProcessingRebalanceMoveKind.ColdEvacuation)
                {
                    coldEvacuationCount = 1;
                }
            }

            if (result.MigrationResult is not null && !result.MigrationResult.Succeeded)
            {
                failedMigrationCount = 1;
            }

            return Empty with
            {
                RebalanceEvaluationCount = result.EvaluatedRebalance ? 1 : 0,
                AcceptedMoveCount = acceptedMoveCount,
                SkippedDecisionCount = skippedDecisionCount,
                DirectHotReliefCount = directHotReliefCount,
                ColdEvacuationCount = coldEvacuationCount,
                FailedMigrationCount = failedMigrationCount,
                ValidationSucceeded = result.Validation.IsValid && result.ProcessingResult.IsValid,
                ProcessingValidationFailedBatchCount = result.ProcessingResult.IsValid ? 0 : 1,
                SkippedReasons = skippedReasons,
                SkippedReasonCounters = skippedReasonCounters,
                AcceptedMovePressures = movePressures
            };
        }
    }
}
