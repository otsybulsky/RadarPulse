using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQuarantineLifecycleStateTests
{
    private static RadarProcessingQuarantineEvidence CreateEvidence(
        int partitionId = 0,
        int shardId = 0,
        long evaluationSequence = 0,
        RadarProcessingTopologyVersion? topologyVersion = null,
        double pressure = 1,
        RadarProcessingPressureBand band = RadarProcessingPressureBand.Hot,
        RadarProcessingHotPartitionClassification observedClassification = RadarProcessingHotPartitionClassification.Quarantined) =>
        new(
            partitionId,
            shardId,
            evaluationSequence,
            topologyVersion ?? RadarProcessingTopologyVersion.Initial,
            new RadarProcessingPressureScore(pressure),
            band,
            observedClassification);

    private static RadarProcessingQuarantineTransition CreateTransition(
        int partitionId = 0,
        int shardId = 0,
        long evaluationSequence = 0,
        RadarProcessingQuarantineEffectiveClassification previousClassification =
            RadarProcessingQuarantineEffectiveClassification.Quarantined,
        RadarProcessingQuarantineEffectiveClassification currentClassification =
            RadarProcessingQuarantineEffectiveClassification.RetryEligible,
        RadarProcessingQuarantineTransitionReason reason =
            RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl,
        long quarantineAgeEvaluations = 0) =>
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
