using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceTelemetryContractTests
{
    [Fact]
    public void TelemetrySummaryCopiesCollectionsAndRejectsDuplicateSkippedReasons()
    {
        var skippedCounters = new List<RadarProcessingRebalanceSkippedReasonCounter>
        {
            new(RadarProcessingRebalanceSkippedReason.NoHotShard, 2)
        };
        var decisions = new List<RadarProcessingRebalanceRecentDecision>
        {
            new(
                decisionId: 1,
                evaluationSequence: 2,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceDecisionKind.NoAction,
                skippedReasons: new[] { RadarProcessingRebalanceSkippedReason.NoHotShard })
        };
        var acceptedMoves = new List<RadarProcessingRebalanceRecentAcceptedMove>
        {
            CreateAcceptedMove()
        };
        var lifecycleTransitions = new List<RadarProcessingRebalanceRecentLifecycleTransition>
        {
            CreateLifecycleTransition()
        };
        var validationFailures = new List<RadarProcessingRebalanceRecentValidationFailure>
        {
            new(
                evaluationSequence: 4,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceValidationError.MigrationFailed)
        };

        var summary = new RadarProcessingRebalanceTelemetrySummary(
            new RadarProcessingRebalanceTelemetryCounters(evaluationCount: 5),
            skippedCounters,
            decisions,
            acceptedMoves,
            validationFailures,
            new RadarProcessingRebalanceRetentionStats(retainedDecisionCount: 1),
            lifecycleTransitions);

        skippedCounters.Add(new RadarProcessingRebalanceSkippedReasonCounter(
            RadarProcessingRebalanceSkippedReason.NoColdTargetShard,
            1));
        decisions.Clear();
        acceptedMoves.Clear();
        lifecycleTransitions.Clear();
        validationFailures.Clear();

        Assert.Equal(5, summary.Counters.EvaluationCount);
        Assert.Single(summary.SkippedReasonCounters);
        Assert.Single(summary.RecentDecisions);
        Assert.Single(summary.RecentLifecycleTransitions);
        Assert.Single(summary.RecentAcceptedMoves);
        Assert.Single(summary.RecentValidationFailures);
        Assert.Equal(1, summary.RetentionStats.RetainedDecisionCount);

        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingRebalanceTelemetrySummary(
                new RadarProcessingRebalanceTelemetryCounters(),
                new[]
                {
                    new RadarProcessingRebalanceSkippedReasonCounter(
                        RadarProcessingRebalanceSkippedReason.NoHotShard,
                        1),
                    new RadarProcessingRebalanceSkippedReasonCounter(
                        RadarProcessingRebalanceSkippedReason.NoHotShard,
                        2)
                },
                Array.Empty<RadarProcessingRebalanceRecentDecision>(),
                Array.Empty<RadarProcessingRebalanceRecentAcceptedMove>(),
                Array.Empty<RadarProcessingRebalanceRecentValidationFailure>(),
                new RadarProcessingRebalanceRetentionStats()));
    }

    [Fact]
    public void EmptyTelemetrySummaryHasEmptyCollections()
    {
        var summary = RadarProcessingRebalanceTelemetrySummary.Empty;

        Assert.Equal(0, summary.Counters.EvaluationCount);
        Assert.Empty(summary.SkippedReasonCounters);
        Assert.Empty(summary.RecentDecisions);
        Assert.Empty(summary.RecentLifecycleTransitions);
        Assert.Empty(summary.RecentAcceptedMoves);
        Assert.Empty(summary.RecentValidationFailures);
        Assert.False(summary.RetentionStats.HasDroppedDetail);
    }

}
