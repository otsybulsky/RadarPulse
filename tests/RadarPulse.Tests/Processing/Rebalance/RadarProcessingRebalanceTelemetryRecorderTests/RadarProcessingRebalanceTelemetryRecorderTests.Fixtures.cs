using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceTelemetryRecorderTests
{
    private static RadarProcessingRebalanceDecision CreateNoActionDecision(
        long decisionId) =>
        RadarProcessingRebalanceDecision.NoAction(
            decisionId,
            evaluationSequence: decisionId,
            RadarProcessingTopologyVersion.Initial,
            pressureWindowSampleCount: 3,
            new[] { RadarProcessingRebalanceSkippedReason.NoHotShard });

    private static RadarProcessingRebalanceDecision CreateAcceptedDecision(
        long decisionId,
        RadarProcessingRebalanceMoveKind moveKind) =>
        RadarProcessingRebalanceDecision.AcceptedMove(
            decisionId,
            evaluationSequence: decisionId,
            RadarProcessingTopologyVersion.Initial,
            pressureWindowSampleCount: 3,
            CreateCandidate(moveKind));

    private static RadarProcessingQuarantineTransition CreateQuarantineTransition(
        RadarProcessingQuarantineTransitionReason reason,
        long evaluationSequence = 1) =>
        new(
            partitionId: 1,
            shardId: 0,
            evaluationSequence,
            RadarProcessingTopologyVersion.Initial,
            RadarProcessingQuarantineEffectiveClassification.Quarantined,
            reason switch
            {
                RadarProcessingQuarantineTransitionReason.EnteredQuarantine or
                    RadarProcessingQuarantineTransitionReason.ReenteredQuarantine =>
                    RadarProcessingQuarantineEffectiveClassification.Quarantined,
                RadarProcessingQuarantineTransitionReason.ClearedBySustainedCooling or
                    RadarProcessingQuarantineTransitionReason.ClearedByEffectiveRelief or
                    RadarProcessingQuarantineTransitionReason.ClearedExplicitly =>
                    RadarProcessingQuarantineEffectiveClassification.None,
                _ => RadarProcessingQuarantineEffectiveClassification.RetryEligible
            },
            reason,
            new RadarProcessingPressureScore(5),
            quarantineAgeEvaluations: evaluationSequence);

    private static RadarProcessingRebalanceCandidate CreateCandidate(
        RadarProcessingRebalanceMoveKind moveKind = RadarProcessingRebalanceMoveKind.DirectHotRelief) =>
        new(
            moveKind,
            partitionId: 1,
            sourceShardId: 0,
            targetShardId: 1,
            new RadarProcessingProjectedPressure(
                new RadarProcessingPressureScore(10),
                new RadarProcessingPressureScore(1),
                new RadarProcessingPressureScore(5),
                new RadarProcessingPressureScore(6)),
            expectedRelief: 5);
}
