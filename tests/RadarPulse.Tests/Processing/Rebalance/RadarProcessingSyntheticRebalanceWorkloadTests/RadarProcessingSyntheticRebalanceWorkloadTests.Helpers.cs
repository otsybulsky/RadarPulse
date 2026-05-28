using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingSyntheticRebalanceWorkloadTests
{
    private const int RetentionStressBatchCount = 16;
    private const int RetentionStressDecisionLimit = 4;

    private static RadarProcessingSyntheticRebalanceWorkloadResult Run(
        RadarProcessingSyntheticRebalanceWorkloadKind kind)
    {
        var workload = RadarProcessingSyntheticRebalanceWorkload.Create(kind);
        return new RadarProcessingSyntheticRebalanceWorkloadRunner().Run(workload);
    }

    private static void AssertTransitionCount(
        RadarProcessingSyntheticRebalanceWorkloadResult result,
        RadarProcessingQuarantineTransitionReason reason,
        int expectedCount) =>
        Assert.Equal(expectedCount, result.CountQuarantineTransitions(reason));

    private static void AssertRetentionBounded(
        RadarProcessingSyntheticRebalanceWorkloadResult result,
        long expectedDecisionCount)
    {
        Assert.True(expectedDecisionCount > RetentionStressDecisionLimit);
        Assert.Equal(RetentionStressDecisionLimit, result.FinalTelemetrySummary.RecentDecisions.Count);
        Assert.Equal(RetentionStressDecisionLimit, result.RetentionStats.RetainedDecisionCount);
        Assert.Equal(expectedDecisionCount - RetentionStressDecisionLimit, result.RetentionStats.DroppedDecisionCount);
        Assert.True(result.RetentionStats.HasDroppedDetail);
    }
}
