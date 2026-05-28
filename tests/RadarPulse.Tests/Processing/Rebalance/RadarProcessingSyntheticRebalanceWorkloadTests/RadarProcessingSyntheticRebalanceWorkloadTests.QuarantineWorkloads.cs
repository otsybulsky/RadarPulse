using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingSyntheticRebalanceWorkloadTests
{
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
}
