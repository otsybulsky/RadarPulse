using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingDirectHotReliefPlannerTests
{
    private static RadarProcessingQuarantineLifecycleTracker CreateRetryEligibleLifecycle(
        int partitionCount,
        int partitionId,
        int shardId,
        double pressure,
        RadarProcessingPressureBand band,
        RadarProcessingTopologyVersion topologyVersion)
    {
        var lifecycle = new RadarProcessingQuarantineLifecycleTracker(
            partitionCount,
            new RadarProcessingQuarantineLifecycleOptions(
                quarantineTtlEvaluations: 2,
                sustainedCoolingSampleCount: 5,
                materialPressureChangeThreshold: 1.0));
        lifecycle.RecordEvidence(
            partitionId,
            shardId,
            evaluationSequence: 0,
            topologyVersion,
            new RadarProcessingPressureScore(pressure),
            band,
            RadarProcessingHotPartitionClassification.Quarantined);
        lifecycle.RecordEvidence(
            partitionId,
            shardId,
            evaluationSequence: 2,
            topologyVersion,
            new RadarProcessingPressureScore(pressure),
            band,
            RadarProcessingHotPartitionClassification.None);

        Assert.True(lifecycle.GetPartition(partitionId).IsRetryEligible);
        return lifecycle;
    }

    private static void Advance(
        RadarProcessingRebalancePolicyState policyState,
        int count)
    {
        for (var i = 0; i < count; i++)
        {
            policyState.AdvanceEvaluation();
        }
    }

}
