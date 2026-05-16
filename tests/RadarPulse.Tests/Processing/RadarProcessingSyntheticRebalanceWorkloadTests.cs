using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingSyntheticRebalanceWorkloadTests
{
    [Fact]
    public void BalancedWorkloadProducesNoAcceptedMoves()
    {
        var result = Run(RadarProcessingSyntheticRebalanceWorkloadKind.Balanced);

        Assert.True(result.ValidationSucceeded);
        Assert.Equal(2, result.BatchCount);
        Assert.Equal(0, result.AcceptedMoveCount);
        Assert.Equal(0, result.DirectHotReliefMoveCount);
        Assert.Equal(0, result.ColdEvacuationMoveCount);
        Assert.Equal(result.InitialTopologyVersion, result.FinalTopologyVersion);
        Assert.Equal(1, result.TopologyVersionCount);
        Assert.True(result.HasSkippedReason(RadarProcessingRebalanceSkippedReason.NoHotShard));
    }

    [Fact]
    public void SustainedHotWorkloadProducesDirectHotRelief()
    {
        var result = Run(RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard);

        Assert.True(result.ValidationSucceeded);
        Assert.True(result.AcceptedMoveCount >= 1);
        Assert.True(result.DirectHotReliefMoveCount >= 1);
        Assert.Equal(0, result.ColdEvacuationMoveCount);
        Assert.True(result.FinalTopologyVersion.Value > result.InitialTopologyVersion.Value);
        Assert.Contains(
            result.Steps,
            step => step.DirectHotReliefDecision?.MoveKind == RadarProcessingRebalanceMoveKind.DirectHotRelief &&
                    step.PublishedMigration);
    }

    [Fact]
    public void IntrinsicHotWorkloadRejectsHotMoveAndPermitsColdEvacuation()
    {
        var result = Run(RadarProcessingSyntheticRebalanceWorkloadKind.IntrinsicHotPartition);

        Assert.True(result.ValidationSucceeded);
        Assert.Equal(1, result.AcceptedMoveCount);
        Assert.Equal(0, result.DirectHotReliefMoveCount);
        Assert.Equal(1, result.ColdEvacuationMoveCount);
        Assert.True(result.HasSkippedReason(RadarProcessingRebalanceSkippedReason.DirectHotPartitionHasNoSafeTarget));
        Assert.True(result.HasSkippedReason(RadarProcessingRebalanceSkippedReason.PartitionClassifiedIntrinsicHot));
        Assert.Contains(
            result.Steps,
            step => step.ColdEvacuationDecision?.MoveKind == RadarProcessingRebalanceMoveKind.ColdEvacuation &&
                    step.PublishedMigration);
    }

    [Fact]
    public void OscillatingSpikeWorkloadDoesNotTriggerChurn()
    {
        var result = Run(RadarProcessingSyntheticRebalanceWorkloadKind.OscillatingSpike);

        Assert.True(result.ValidationSucceeded);
        Assert.Equal(3, result.BatchCount);
        Assert.Equal(0, result.AcceptedMoveCount);
        Assert.Equal(result.InitialTopologyVersion, result.FinalTopologyVersion);
        Assert.True(result.HasSkippedReason(RadarProcessingRebalanceSkippedReason.NoSustainedPressure));
        Assert.True(result.HasSkippedReason(RadarProcessingRebalanceSkippedReason.NoHotShard));
    }

    [Fact]
    public void CooldownStormWorkloadRecordsSkippedCooldownReasons()
    {
        var result = Run(RadarProcessingSyntheticRebalanceWorkloadKind.CooldownStorm);

        Assert.True(result.ValidationSucceeded);
        Assert.Equal(2, result.BatchCount);
        Assert.Equal(1, result.AcceptedMoveCount);
        Assert.Equal(1, result.DirectHotReliefMoveCount);
        Assert.True(result.HasSkippedReason(RadarProcessingRebalanceSkippedReason.CandidatePartitionInCooldown));
        Assert.Contains(
            result.Steps,
            step => step.DirectHotReliefDecision?.PolicyRejections.Contains(
                RadarProcessingRebalancePolicyRejection.PartitionInCooldown) == true);
    }

    [Fact]
    public void WorkloadFactoryRejectsUnknownKind()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingSyntheticRebalanceWorkload.Create(
                (RadarProcessingSyntheticRebalanceWorkloadKind)255));
    }

    private static RadarProcessingSyntheticRebalanceWorkloadResult Run(
        RadarProcessingSyntheticRebalanceWorkloadKind kind)
    {
        var workload = RadarProcessingSyntheticRebalanceWorkload.Create(kind);
        return new RadarProcessingSyntheticRebalanceWorkloadRunner().Run(workload);
    }
}
