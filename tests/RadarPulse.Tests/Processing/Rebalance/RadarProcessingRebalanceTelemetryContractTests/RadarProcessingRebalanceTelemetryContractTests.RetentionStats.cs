using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceTelemetryContractTests
{
    [Fact]
    public void RetentionStatsExposeDroppedDetail()
    {
        var stats = new RadarProcessingRebalanceRetentionStats(
            retainedDecisionCount: 1,
            droppedDecisionCount: 2,
            retainedLifecycleTransitionCount: 3,
            droppedLifecycleTransitionCount: 4,
            retainedAcceptedMoveCount: 5,
            droppedAcceptedMoveCount: 6,
            retainedValidationFailureCount: 7,
            droppedValidationFailureCount: 8);

        Assert.Equal(1, stats.RetainedDecisionCount);
        Assert.Equal(2, stats.DroppedDecisionCount);
        Assert.Equal(3, stats.RetainedLifecycleTransitionCount);
        Assert.Equal(4, stats.DroppedLifecycleTransitionCount);
        Assert.Equal(5, stats.RetainedAcceptedMoveCount);
        Assert.Equal(6, stats.DroppedAcceptedMoveCount);
        Assert.Equal(7, stats.RetainedValidationFailureCount);
        Assert.Equal(8, stats.DroppedValidationFailureCount);
        Assert.True(stats.HasDroppedDetail);
        Assert.False(new RadarProcessingRebalanceRetentionStats().HasDroppedDetail);
    }

    [Fact]
    public void RetentionStatsRejectInvalidCounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRetentionStats(retainedDecisionCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRetentionStats(droppedLifecycleTransitionCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRetentionStats(droppedValidationFailureCount: -1));
    }

}
