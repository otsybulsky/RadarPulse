using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceDecisionTests
{
    private static RadarProcessingRebalanceCandidate CreateCandidate(
        RadarProcessingRebalanceMoveKind moveKind = RadarProcessingRebalanceMoveKind.ColdEvacuation,
        int partitionId = 0,
        int sourceShardId = 0,
        int targetShardId = 1,
        double expectedRelief = 1.0,
        double sourceBefore = 4.0,
        double targetBefore = 1.0,
        double projectedSourceAfter = 3.0,
        double projectedTargetAfter = 2.0) =>
        new(
            moveKind,
            partitionId,
            sourceShardId,
            targetShardId,
            new RadarProcessingProjectedPressure(
                new RadarProcessingPressureScore(sourceBefore),
                new RadarProcessingPressureScore(targetBefore),
                new RadarProcessingPressureScore(projectedSourceAfter),
                new RadarProcessingPressureScore(projectedTargetAfter)),
            expectedRelief);
}
