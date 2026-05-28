using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalancePolicyStateTests
{
    [Fact]
    public void OptionsRejectInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceOptions(budgetWindowEvaluationCount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceOptions(globalMoveBudgetPerWindow: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceOptions(minimumPartitionResidencyEvaluations: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceOptions(partitionMoveCooldownEvaluations: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceOptions(minimumProjectedBenefit: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceOptions(targetHeadroomThreshold: double.PositiveInfinity));
    }

    [Fact]
    public void MovePolicyInputRejectsInvalidShape()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateInput(partitionId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateInput(sourceShardId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateInput(targetShardId: -1));
        Assert.Throws<ArgumentException>(() =>
            CreateInput(sourceShardId: 1, targetShardId: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateInput(projectedBenefit: double.NaN));
    }
}
