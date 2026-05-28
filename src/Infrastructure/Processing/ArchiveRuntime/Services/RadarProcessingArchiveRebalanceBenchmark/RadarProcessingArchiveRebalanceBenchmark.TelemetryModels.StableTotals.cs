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
        public bool HasSameStableTotals(ArchiveIterationTelemetry other) =>
            ExaminedFileCount == other.ExaminedFileCount &&
            SkippedFileCount == other.SkippedFileCount &&
            PublishedFileCount == other.PublishedFileCount &&
            FileSizeBytes == other.FileSizeBytes &&
            CompressedRecordCount == other.CompressedRecordCount &&
            CompressedBytes == other.CompressedBytes &&
            DecompressedBytes == other.DecompressedBytes &&
            BatchCount == other.BatchCount &&
            EventCount == other.EventCount &&
            PayloadBytes == other.PayloadBytes &&
            PayloadValueCount == other.PayloadValueCount &&
            RawValueChecksum == other.RawValueChecksum &&
            TopologyVersionCount == other.TopologyVersionCount &&
            RebalanceEvaluationCount == other.RebalanceEvaluationCount &&
            AcceptedMoveCount == other.AcceptedMoveCount &&
            SkippedDecisionCount == other.SkippedDecisionCount &&
            DirectHotReliefCount == other.DirectHotReliefCount &&
            ColdEvacuationCount == other.ColdEvacuationCount &&
            FailedMigrationCount == other.FailedMigrationCount &&
            ValidationSucceeded == other.ValidationSucceeded &&
            ValidationChecksum == other.ValidationChecksum &&
            ProcessingValidationFailedBatchCount == other.ProcessingValidationFailedBatchCount &&
            HasSameSkippedReasonCounters(SkippedReasonCounters, other.SkippedReasonCounters) &&
            HasSameRetentionStats(RetentionStats, other.RetentionStats);

        private static bool HasSameSkippedReasonCounters(
            IReadOnlyList<RadarProcessingRebalanceSkippedReasonCounter>? current,
            IReadOnlyList<RadarProcessingRebalanceSkippedReasonCounter>? other)
        {
            var currentCount = current?.Count ?? 0;
            if (currentCount != (other?.Count ?? 0))
            {
                return false;
            }

            if (currentCount == 0)
            {
                return true;
            }

            var currentSorted = current!.OrderBy(counter => counter.Reason).ToArray();
            var otherSorted = other!.OrderBy(counter => counter.Reason).ToArray();
            for (var index = 0; index < currentSorted.Length; index++)
            {
                if (currentSorted[index].Reason != otherSorted[index].Reason ||
                    currentSorted[index].Count != otherSorted[index].Count)
                {
                    return false;
                }
            }

            return true;
        }

        private static RadarProcessingRebalanceRetentionStats AddRetentionStats(
            RadarProcessingRebalanceRetentionStats current,
            RadarProcessingRebalanceRetentionStats other) =>
            new(
                Math.Max(current.RetainedDecisionCount, other.RetainedDecisionCount),
                checked(current.DroppedDecisionCount + other.DroppedDecisionCount),
                Math.Max(
                    current.RetainedLifecycleTransitionCount,
                    other.RetainedLifecycleTransitionCount),
                checked(current.DroppedLifecycleTransitionCount + other.DroppedLifecycleTransitionCount),
                Math.Max(current.RetainedAcceptedMoveCount, other.RetainedAcceptedMoveCount),
                checked(current.DroppedAcceptedMoveCount + other.DroppedAcceptedMoveCount),
                Math.Max(current.RetainedValidationFailureCount, other.RetainedValidationFailureCount),
                checked(current.DroppedValidationFailureCount + other.DroppedValidationFailureCount));

        private static bool HasSameRetentionStats(
            RadarProcessingRebalanceRetentionStats current,
            RadarProcessingRebalanceRetentionStats other) =>
            current.RetainedDecisionCount == other.RetainedDecisionCount &&
            current.DroppedDecisionCount == other.DroppedDecisionCount &&
            current.RetainedLifecycleTransitionCount == other.RetainedLifecycleTransitionCount &&
            current.DroppedLifecycleTransitionCount == other.DroppedLifecycleTransitionCount &&
            current.RetainedAcceptedMoveCount == other.RetainedAcceptedMoveCount &&
            current.DroppedAcceptedMoveCount == other.DroppedAcceptedMoveCount &&
            current.RetainedValidationFailureCount == other.RetainedValidationFailureCount &&
            current.DroppedValidationFailureCount == other.DroppedValidationFailureCount;
    }
}
