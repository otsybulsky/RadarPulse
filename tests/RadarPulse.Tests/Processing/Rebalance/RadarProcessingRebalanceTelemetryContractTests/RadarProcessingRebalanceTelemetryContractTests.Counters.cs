using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceTelemetryContractTests
{
    [Fact]
    public void TelemetryCountersCarryNonNegativeAggregateValues()
    {
        var counters = new RadarProcessingRebalanceTelemetryCounters(
            evaluationCount: 10,
            noActionDecisionCount: 4,
            acceptedMoveCount: 3,
            rejectedCandidateCount: 5,
            directHotReliefMoveCount: 2,
            coldEvacuationMoveCount: 1,
            failedMigrationCount: 1,
            validationFailureCount: 2,
            quarantineEntryCount: 3,
            quarantineClearCount: 4,
            quarantineRetryCount: 5,
            quarantineReentryCount: 6);

        Assert.Equal(10, counters.EvaluationCount);
        Assert.Equal(4, counters.NoActionDecisionCount);
        Assert.Equal(3, counters.AcceptedMoveCount);
        Assert.Equal(5, counters.RejectedCandidateCount);
        Assert.Equal(2, counters.DirectHotReliefMoveCount);
        Assert.Equal(1, counters.ColdEvacuationMoveCount);
        Assert.Equal(1, counters.FailedMigrationCount);
        Assert.Equal(2, counters.ValidationFailureCount);
        Assert.Equal(3, counters.QuarantineEntryCount);
        Assert.Equal(4, counters.QuarantineClearCount);
        Assert.Equal(5, counters.QuarantineRetryCount);
        Assert.Equal(6, counters.QuarantineReentryCount);
    }

    [Fact]
    public void TelemetryCountersRejectInvalidCounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceTelemetryCounters(evaluationCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceTelemetryCounters(acceptedMoveCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceTelemetryCounters(acceptedMoveCount: 1, directHotReliefMoveCount: 1, coldEvacuationMoveCount: 1));
    }

    [Fact]
    public void SkippedReasonCounterRejectsInvalidShape()
    {
        var counter = new RadarProcessingRebalanceSkippedReasonCounter(
            RadarProcessingRebalanceSkippedReason.NoHotShard,
            count: 7);

        Assert.Equal(RadarProcessingRebalanceSkippedReason.NoHotShard, counter.Reason);
        Assert.Equal(7, counter.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceSkippedReasonCounter(RadarProcessingRebalanceSkippedReason.None, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceSkippedReasonCounter((RadarProcessingRebalanceSkippedReason)255, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceSkippedReasonCounter(RadarProcessingRebalanceSkippedReason.NoHotShard, -1));
    }

}
