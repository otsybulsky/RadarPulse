using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingSyntheticRebalanceBenchmark
{
    private readonly partial record struct IterationTelemetry
    {
        private static void AddDecision(
            RadarProcessingRebalanceDecision? decision,
            ref List<RadarProcessingRebalanceSkippedReason>? skippedReasons,
            ref long skippedDecisionCount)
        {
            if (decision is null || decision.HasAcceptedMove)
            {
                return;
            }

            skippedDecisionCount = checked(skippedDecisionCount + 1);
            foreach (var reason in decision.SkippedReasons)
            {
                AddSkippedReason(ref skippedReasons, reason);
            }
        }

        private static void AddSkippedReason(
            ref List<RadarProcessingRebalanceSkippedReason>? skippedReasons,
            RadarProcessingRebalanceSkippedReason reason)
        {
            skippedReasons ??= new List<RadarProcessingRebalanceSkippedReason>();
            if (!skippedReasons.Contains(reason))
            {
                skippedReasons.Add(reason);
            }
        }

        private static RadarProcessingSyntheticRebalanceMovePressure CreateMovePressure(
            RadarProcessingRebalanceDecision decision) =>
            new(
                decision.MoveKind,
                decision.ProjectedPressure.SourceShardBefore.Value,
                decision.ProjectedPressure.TargetShardBefore.Value,
                decision.ProjectedPressure.SourceShardAfter.Value,
                decision.ProjectedPressure.TargetShardAfter.Value,
                decision.ExpectedRelief);

        private static ulong ComputeChecksum(
            RadarProcessingMetrics metrics,
            long topologyVersionCount,
            long rebalanceEvaluationCount,
            long acceptedMoveCount,
            long skippedDecisionCount,
            long directHotReliefCount,
            long coldEvacuationCount,
            long failedMigrationCount,
            bool validationSucceeded)
        {
            var checksum = ChecksumInitial;
            checksum = AppendInt64(checksum, metrics.ProcessedBatchCount);
            checksum = AppendInt64(checksum, metrics.ProcessedStreamEventCount);
            checksum = AppendInt64(checksum, metrics.ProcessedPayloadValueCount);
            checksum = AppendInt64(checksum, metrics.ActiveSourceCount);
            checksum = AppendInt64(checksum, metrics.RawValueChecksum);
            checksum = AppendUInt64(checksum, metrics.ProcessingChecksum);
            checksum = AppendInt64(checksum, topologyVersionCount);
            checksum = AppendInt64(checksum, rebalanceEvaluationCount);
            checksum = AppendInt64(checksum, acceptedMoveCount);
            checksum = AppendInt64(checksum, skippedDecisionCount);
            checksum = AppendInt64(checksum, directHotReliefCount);
            checksum = AppendInt64(checksum, coldEvacuationCount);
            checksum = AppendInt64(checksum, failedMigrationCount);
            return AppendInt32(checksum, validationSucceeded ? 1 : 0);
        }
    }
}
