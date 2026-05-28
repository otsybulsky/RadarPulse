using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalancePolicyStateTests
{
    [Fact]
    public void ProjectedBenefitBelowThresholdIsRejected()
    {
        var state = new RadarProcessingRebalancePolicyState(
            partitionCount: 4,
            shardCount: 3,
            CreateOptions(minimumProjectedBenefit: 0.2));

        var result = state.EvaluateMove(CreateInput(projectedBenefit: 0.1));

        Assert.False(result.IsAllowed);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.InsufficientProjectedBenefit,
            result.Rejections);
    }

    [Fact]
    public void TargetHeadroomGateRejectsProjectedPressure()
    {
        var state = new RadarProcessingRebalancePolicyState(
            partitionCount: 4,
            shardCount: 3,
            CreateOptions(targetHeadroomThreshold: 10.0));

        var result = state.EvaluateMove(CreateInput(targetProjectedPressure: 11.0));

        Assert.False(result.IsAllowed);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.TargetHeadroomExceeded,
            result.Rejections);
    }
}
