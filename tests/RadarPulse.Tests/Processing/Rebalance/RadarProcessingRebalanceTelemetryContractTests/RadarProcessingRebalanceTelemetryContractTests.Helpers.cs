using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceTelemetryContractTests
{
    private static RadarProcessingProjectedPressure CreateProjectedPressure() =>
        new(
            new RadarProcessingPressureScore(10),
            new RadarProcessingPressureScore(1),
            new RadarProcessingPressureScore(5),
            new RadarProcessingPressureScore(6));

    private static RadarProcessingRebalanceRecentAcceptedMove CreateAcceptedMove(
        long decisionId = 1,
        RadarProcessingRebalanceMoveKind moveKind = RadarProcessingRebalanceMoveKind.DirectHotRelief,
        int partitionId = 1,
        int sourceShardId = 0,
        int targetShardId = 1,
        double expectedRelief = 1.0) =>
        new(
            decisionId,
            evaluationSequence: 2,
            RadarProcessingTopologyVersion.Initial,
            resultTopologyVersion: null,
            moveKind,
            partitionId,
            sourceShardId,
            targetShardId,
            CreateProjectedPressure(),
            expectedRelief);

    private static RadarProcessingRebalanceRecentLifecycleTransition CreateLifecycleTransition(
        int partitionId = 1,
        int shardId = 0,
        long evaluationSequence = 2,
        RadarProcessingQuarantineEffectiveClassification previousClassification =
            RadarProcessingQuarantineEffectiveClassification.Quarantined,
        RadarProcessingQuarantineEffectiveClassification currentClassification =
            RadarProcessingQuarantineEffectiveClassification.RetryEligible,
        RadarProcessingQuarantineTransitionReason reason =
            RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl,
        long quarantineAgeEvaluations = 3) =>
        new(
            partitionId,
            shardId,
            evaluationSequence,
            RadarProcessingTopologyVersion.Initial,
            previousClassification,
            currentClassification,
            reason,
            new RadarProcessingPressureScore(1),
            quarantineAgeEvaluations);
}
