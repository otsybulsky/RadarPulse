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
        private static void AddDecision(
            RadarProcessingRebalanceDecision? decision,
            ref List<RadarProcessingRebalanceSkippedReason>? skippedReasons,
            ref List<RadarProcessingRebalanceSkippedReasonCounter>? skippedReasonCounters,
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
                AddSkippedReasonCounter(ref skippedReasonCounters, reason, count: 1);
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

        private static void AddSkippedReasonCounter(
            ref List<RadarProcessingRebalanceSkippedReasonCounter>? skippedReasonCounters,
            RadarProcessingRebalanceSkippedReason reason,
            long count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            if (count == 0)
            {
                return;
            }

            skippedReasonCounters ??= new List<RadarProcessingRebalanceSkippedReasonCounter>();
            for (var index = 0; index < skippedReasonCounters.Count; index++)
            {
                if (skippedReasonCounters[index].Reason != reason)
                {
                    continue;
                }

                skippedReasonCounters[index] = new RadarProcessingRebalanceSkippedReasonCounter(
                    reason,
                    checked(skippedReasonCounters[index].Count + count));
                return;
            }

            skippedReasonCounters.Add(new RadarProcessingRebalanceSkippedReasonCounter(reason, count));
        }
    }
}
