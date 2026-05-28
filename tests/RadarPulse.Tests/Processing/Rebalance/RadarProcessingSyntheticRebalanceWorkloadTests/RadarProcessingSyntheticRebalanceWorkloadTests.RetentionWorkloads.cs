using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingSyntheticRebalanceWorkloadTests
{
    [Fact]
    public void LongNoHotShardWorkloadBoundsRetainedDecisionDetail()
    {
        var result = Run(RadarProcessingSyntheticRebalanceWorkloadKind.LongNoHotShard);
        var expectedDecisionCount = RetentionStressBatchCount * 2;

        Assert.True(result.ValidationSucceeded);
        Assert.Equal(RetentionStressBatchCount, result.BatchCount);
        Assert.Equal(0, result.AcceptedMoveCount);
        Assert.Equal(expectedDecisionCount, result.FinalTelemetrySummary.Counters.EvaluationCount);
        Assert.Equal(expectedDecisionCount, result.FinalTelemetrySummary.Counters.NoActionDecisionCount);
        Assert.Equal(expectedDecisionCount, result.CountSkippedReason(RadarProcessingRebalanceSkippedReason.NoHotShard));
        AssertRetentionBounded(result, expectedDecisionCount);
    }

    [Fact]
    public void LongCooldownRejectionWorkloadBoundsRetainedRejectedDetail()
    {
        var result = Run(RadarProcessingSyntheticRebalanceWorkloadKind.LongCooldownRejection);

        Assert.True(result.ValidationSucceeded);
        Assert.Equal(RetentionStressBatchCount, result.BatchCount);
        Assert.Equal(1, result.AcceptedMoveCount);
        Assert.True(result.CountSkippedReason(RadarProcessingRebalanceSkippedReason.CandidatePartitionInCooldown) > 0);
        Assert.True(result.CountSkippedReason(RadarProcessingRebalanceSkippedReason.GlobalMoveBudgetExhausted) > 0);
        AssertRetentionBounded(result, result.FinalTelemetrySummary.Counters.EvaluationCount);
    }

    [Fact]
    public void LongUnsafeTargetWorkloadAggregatesTargetSafetyReason()
    {
        var result = Run(RadarProcessingSyntheticRebalanceWorkloadKind.LongUnsafeTargetRejection);

        Assert.True(result.ValidationSucceeded);
        Assert.Equal(RetentionStressBatchCount, result.BatchCount);
        Assert.Equal(0, result.AcceptedMoveCount);
        Assert.True(result.CountSkippedReason(RadarProcessingRebalanceSkippedReason.TargetWouldBecomeHot) > 0);
        Assert.True(result.CountSkippedReason(RadarProcessingRebalanceSkippedReason.ColdEvacuationInsufficientBenefit) > 0);
        AssertRetentionBounded(result, result.FinalTelemetrySummary.Counters.EvaluationCount);
    }

    [Fact]
    public void LongMixedSkippedReasonsWorkloadAggregatesDistinctReasons()
    {
        var result = Run(RadarProcessingSyntheticRebalanceWorkloadKind.LongMixedSkippedReasons);

        Assert.True(result.ValidationSucceeded);
        Assert.Equal(RetentionStressBatchCount, result.BatchCount);
        Assert.True(result.HasSkippedReason(RadarProcessingRebalanceSkippedReason.NoHotShard));
        Assert.True(result.HasSkippedReason(RadarProcessingRebalanceSkippedReason.CandidatePartitionInCooldown));
        Assert.True(result.HasSkippedReason(RadarProcessingRebalanceSkippedReason.GlobalMoveBudgetExhausted));
        Assert.True(result.HasSkippedReason(RadarProcessingRebalanceSkippedReason.TargetWouldBecomeHot));
        Assert.True(result.FinalTelemetrySummary.SkippedReasonCounters.Count >= 4);
        AssertRetentionBounded(result, result.FinalTelemetrySummary.Counters.EvaluationCount);
    }

    [Fact]
    public void CountersOnlyRetentionWorkloadDropsAllDecisionDetailButKeepsCounters()
    {
        var result = Run(RadarProcessingSyntheticRebalanceWorkloadKind.CountersOnlyRetention);
        var expectedDecisionCount = RetentionStressBatchCount * 2;

        Assert.True(result.ValidationSucceeded);
        Assert.Equal(expectedDecisionCount, result.FinalTelemetrySummary.Counters.EvaluationCount);
        Assert.Equal(expectedDecisionCount, result.FinalTelemetrySummary.Counters.NoActionDecisionCount);
        Assert.Equal(expectedDecisionCount, result.CountSkippedReason(RadarProcessingRebalanceSkippedReason.NoHotShard));
        Assert.Empty(result.FinalTelemetrySummary.RecentDecisions);
        Assert.Equal(0, result.RetentionStats.RetainedDecisionCount);
        Assert.Equal(expectedDecisionCount, result.RetentionStats.DroppedDecisionCount);
        Assert.True(result.RetentionStats.HasDroppedDetail);
    }
}
