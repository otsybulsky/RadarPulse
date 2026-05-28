using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQuarantineLifecycleEvaluatorTests
{
    private static RadarProcessingQuarantineLifecycleEvaluator CreateEvaluator(
        int quarantineTtlEvaluations,
        int sustainedCoolingSampleCount,
        double materialPressureChangeThreshold) =>
        new(
            new RadarProcessingQuarantineLifecycleOptions(
                quarantineTtlEvaluations,
                sustainedCoolingSampleCount,
                materialPressureChangeThreshold));

    private static RadarProcessingQuarantineLifecycleState CreateQuarantinedState(
        int partitionId,
        int shardId = 0,
        long evaluationSequence = 0,
        double pressure = 100,
        RadarProcessingPressureBand band = RadarProcessingPressureBand.Hot) =>
        RadarProcessingQuarantineLifecycleState
            .Unclassified(partitionId)
            .EnterQuarantine(
                CreateEvidence(
                    partitionId,
                    shardId,
                    evaluationSequence,
                    pressure: pressure,
                    band: band));

    private static RadarProcessingQuarantineEvidence CreateEvidence(
        int partitionId = 0,
        int shardId = 0,
        long evaluationSequence = 0,
        RadarProcessingTopologyVersion? topologyVersion = null,
        double pressure = 1,
        RadarProcessingPressureBand band = RadarProcessingPressureBand.Hot,
        RadarProcessingHotPartitionClassification observedClassification =
            RadarProcessingHotPartitionClassification.Quarantined) =>
        new(
            partitionId,
            shardId,
            evaluationSequence,
            topologyVersion ?? RadarProcessingTopologyVersion.Initial,
            new RadarProcessingPressureScore(pressure),
            band,
            observedClassification);

    private static void AssertTransition(
        RadarProcessingQuarantineTransition transition,
        RadarProcessingQuarantineEffectiveClassification previousClassification,
        RadarProcessingQuarantineEffectiveClassification currentClassification,
        RadarProcessingQuarantineTransitionReason reason,
        long quarantineAgeEvaluations)
    {
        Assert.Equal(1, transition.PartitionId);
        Assert.Equal(previousClassification, transition.PreviousClassification);
        Assert.Equal(currentClassification, transition.CurrentClassification);
        Assert.Equal(reason, transition.Reason);
        Assert.Equal(quarantineAgeEvaluations, transition.QuarantineAgeEvaluations);
    }
}
