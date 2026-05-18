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
    public void QuarantineTtlRetryWorkloadMarksRetryEligibleWithoutClearing()
    {
        var result = Run(RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineTtlRetry);

        Assert.True(result.ValidationSucceeded);
        Assert.Equal(2, result.BatchCount);
        Assert.Equal(0, result.AcceptedMoveCount);
        Assert.Equal(1, result.QuarantineEntryCount);
        Assert.Equal(1, result.QuarantineRetryCount);
        Assert.Equal(0, result.QuarantineClearCount);
        Assert.Equal(0, result.QuarantineReentryCount);
        AssertTransitionCount(result, RadarProcessingQuarantineTransitionReason.EnteredQuarantine, 1);
        AssertTransitionCount(result, RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl, 1);
        Assert.True(result.FinalQuarantineStates[0].IsRetryEligible);
        Assert.True(result.HasSkippedReason(RadarProcessingRebalanceSkippedReason.PartitionQuarantined));
    }

    [Fact]
    public void QuarantineSustainedCoolingWorkloadClearsQuarantine()
    {
        var result = Run(RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSustainedCoolingClear);

        Assert.True(result.ValidationSucceeded);
        Assert.Equal(3, result.BatchCount);
        Assert.Equal(0, result.AcceptedMoveCount);
        Assert.Equal(1, result.QuarantineEntryCount);
        Assert.Equal(1, result.QuarantineClearCount);
        Assert.Equal(0, result.QuarantineRetryCount);
        Assert.Equal(0, result.QuarantineReentryCount);
        AssertTransitionCount(result, RadarProcessingQuarantineTransitionReason.EnteredQuarantine, 1);
        AssertTransitionCount(result, RadarProcessingQuarantineTransitionReason.ClearedBySustainedCooling, 1);
        Assert.Equal(
            RadarProcessingQuarantineEffectiveClassification.None,
            result.FinalQuarantineStates[0].EffectiveClassification);
        Assert.False(result.FinalQuarantineStates[0].HasQuarantineEvidence);
    }

    [Fact]
    public void QuarantinePressureChangeWorkloadMarksRetryEligible()
    {
        var result = Run(RadarProcessingSyntheticRebalanceWorkloadKind.QuarantinePressureChangeRetry);

        Assert.True(result.ValidationSucceeded);
        Assert.Equal(2, result.BatchCount);
        Assert.Equal(0, result.AcceptedMoveCount);
        Assert.Equal(1, result.QuarantineEntryCount);
        Assert.Equal(1, result.QuarantineRetryCount);
        Assert.Equal(0, result.QuarantineClearCount);
        Assert.Equal(0, result.QuarantineReentryCount);
        AssertTransitionCount(result, RadarProcessingQuarantineTransitionReason.EnteredQuarantine, 1);
        AssertTransitionCount(
            result,
            RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByPressureChange,
            1);
        Assert.True(result.FinalQuarantineStates[0].IsRetryEligible);
    }

    [Fact]
    public void QuarantineRetryReentryWorkloadReentersAfterUnsafeRetry()
    {
        var result = Run(RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineRetryReentry);

        Assert.True(result.ValidationSucceeded);
        Assert.Equal(2, result.BatchCount);
        Assert.Equal(0, result.AcceptedMoveCount);
        Assert.Equal(1, result.QuarantineEntryCount);
        Assert.Equal(1, result.QuarantineRetryCount);
        Assert.Equal(0, result.QuarantineClearCount);
        Assert.Equal(1, result.QuarantineReentryCount);
        AssertTransitionCount(result, RadarProcessingQuarantineTransitionReason.EnteredQuarantine, 1);
        AssertTransitionCount(result, RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl, 1);
        AssertTransitionCount(result, RadarProcessingQuarantineTransitionReason.ReenteredQuarantine, 1);
        Assert.True(result.FinalQuarantineStates[0].IsQuarantined);
        Assert.True(result.HasSkippedReason(RadarProcessingRebalanceSkippedReason.PartitionQuarantined));
    }

    [Fact]
    public void QuarantineSuccessfulReliefWorkloadClearsAfterAcceptedRetryMove()
    {
        var result = Run(RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSuccessfulReliefClear);

        Assert.True(result.ValidationSucceeded);
        Assert.Equal(2, result.BatchCount);
        Assert.Equal(1, result.AcceptedMoveCount);
        Assert.Equal(1, result.DirectHotReliefMoveCount);
        Assert.Equal(0, result.ColdEvacuationMoveCount);
        Assert.Equal(1, result.QuarantineEntryCount);
        Assert.Equal(1, result.QuarantineRetryCount);
        Assert.Equal(1, result.QuarantineClearCount);
        Assert.Equal(0, result.QuarantineReentryCount);
        AssertTransitionCount(result, RadarProcessingQuarantineTransitionReason.EnteredQuarantine, 1);
        AssertTransitionCount(result, RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl, 1);
        AssertTransitionCount(result, RadarProcessingQuarantineTransitionReason.ClearedByEffectiveRelief, 1);
        Assert.Equal(
            RadarProcessingQuarantineEffectiveClassification.MovableHot,
            result.FinalQuarantineStates[0].EffectiveClassification);
        Assert.False(result.FinalQuarantineStates[0].HasQuarantineEvidence);
        Assert.True(result.FinalTopologyVersion.Value > result.InitialTopologyVersion.Value);
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

    private static void AssertTransitionCount(
        RadarProcessingSyntheticRebalanceWorkloadResult result,
        RadarProcessingQuarantineTransitionReason reason,
        int expectedCount) =>
        Assert.Equal(expectedCount, result.CountQuarantineTransitions(reason));
}
