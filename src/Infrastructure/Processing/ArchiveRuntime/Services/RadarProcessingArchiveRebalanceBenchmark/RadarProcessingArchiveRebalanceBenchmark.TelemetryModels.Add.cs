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
        public ArchiveIterationTelemetry Add(ArchiveIterationTelemetry other)
        {
            var skippedReasons = SkippedReasons;
            if (other.SkippedReasons is { Count: > 0 } otherSkippedReasons)
            {
                foreach (var reason in otherSkippedReasons)
                {
                    AddSkippedReason(ref skippedReasons, reason);
                }
            }

            var skippedReasonCounters = SkippedReasonCounters;
            if (other.SkippedReasonCounters is { Count: > 0 } otherSkippedReasonCounters)
            {
                foreach (var counter in otherSkippedReasonCounters)
                {
                    AddSkippedReasonCounter(ref skippedReasonCounters, counter.Reason, counter.Count);
                }
            }

            var movePressures = AcceptedMovePressures;
            if (other.AcceptedMovePressures is { Count: > 0 } otherMovePressures)
            {
                movePressures ??= new List<RadarProcessingSyntheticRebalanceMovePressure>(
                    otherMovePressures.Count);
                movePressures.AddRange(otherMovePressures);
            }

            return this with
            {
                ExaminedFileCount = checked(ExaminedFileCount + other.ExaminedFileCount),
                SkippedFileCount = checked(SkippedFileCount + other.SkippedFileCount),
                PublishedFileCount = checked(PublishedFileCount + other.PublishedFileCount),
                FileSizeBytes = FileSizeBytes == 0 ? other.FileSizeBytes : FileSizeBytes,
                CompressedRecordCount = checked(CompressedRecordCount + other.CompressedRecordCount),
                CompressedBytes = checked(CompressedBytes + other.CompressedBytes),
                DecompressedBytes = checked(DecompressedBytes + other.DecompressedBytes),
                BatchCount = checked(BatchCount + other.BatchCount),
                EventCount = checked(EventCount + other.EventCount),
                PayloadBytes = checked(PayloadBytes + other.PayloadBytes),
                PayloadValueCount = checked(PayloadValueCount + other.PayloadValueCount),
                RawValueChecksum = checked(RawValueChecksum + other.RawValueChecksum),
                TopologyVersionCount = Math.Max(TopologyVersionCount, other.TopologyVersionCount),
                RebalanceEvaluationCount = checked(RebalanceEvaluationCount + other.RebalanceEvaluationCount),
                AcceptedMoveCount = checked(AcceptedMoveCount + other.AcceptedMoveCount),
                SkippedDecisionCount = checked(SkippedDecisionCount + other.SkippedDecisionCount),
                DirectHotReliefCount = checked(DirectHotReliefCount + other.DirectHotReliefCount),
                ColdEvacuationCount = checked(ColdEvacuationCount + other.ColdEvacuationCount),
                FailedMigrationCount = checked(FailedMigrationCount + other.FailedMigrationCount),
                ValidationSucceeded = ValidationSucceeded && other.ValidationSucceeded,
                ValidationChecksum = AppendUInt64(ValidationChecksum, other.ValidationChecksum),
                ProcessingValidationFailedBatchCount = checked(
                    ProcessingValidationFailedBatchCount + other.ProcessingValidationFailedBatchCount),
                SkippedReasons = skippedReasons,
                SkippedReasonCounters = skippedReasonCounters,
                AcceptedMovePressures = movePressures,
                RetentionStats = AddRetentionStats(RetentionStats, other.RetentionStats),
                ProcessingElapsed = ProcessingElapsed + other.ProcessingElapsed,
                ProcessingCallbackAllocatedBytes = checked(
                    ProcessingCallbackAllocatedBytes + other.ProcessingCallbackAllocatedBytes),
                QueueTelemetry = AddQueueTelemetry(QueueTelemetry, other.QueueTelemetry),
                RetentionTelemetry = AddRetentionTelemetry(RetentionTelemetry, other.RetentionTelemetry),
                OverlapTelemetry = AddOverlapTelemetry(OverlapTelemetry, other.OverlapTelemetry)
            };
        }
    }
}
