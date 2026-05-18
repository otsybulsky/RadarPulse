using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingRebalanceTelemetryContractTests
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

    [Fact]
    public void RecentDecisionCopiesReasonCollections()
    {
        var skippedReasons = new List<RadarProcessingRebalanceSkippedReason>
        {
            RadarProcessingRebalanceSkippedReason.NoHotShard
        };
        var policyRejections = new List<RadarProcessingRebalancePolicyRejection>
        {
            RadarProcessingRebalancePolicyRejection.GlobalMoveBudgetExhausted
        };

        var decision = new RadarProcessingRebalanceRecentDecision(
            decisionId: 3,
            evaluationSequence: 5,
            RadarProcessingTopologyVersion.Initial,
            RadarProcessingRebalanceDecisionKind.RejectedCandidate,
            RadarProcessingRebalanceMoveKind.DirectHotRelief,
            partitionId: 1,
            sourceShardId: 2,
            targetShardId: 3,
            skippedReasons,
            policyRejections);

        skippedReasons.Add(RadarProcessingRebalanceSkippedReason.NoColdTargetShard);
        policyRejections.Add(RadarProcessingRebalancePolicyRejection.TargetHeadroomExceeded);

        Assert.Equal(3, decision.DecisionId);
        Assert.Equal(5, decision.EvaluationSequence);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, decision.TopologyVersion);
        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, decision.Kind);
        Assert.Equal(RadarProcessingRebalanceMoveKind.DirectHotRelief, decision.MoveKind);
        Assert.Equal(1, decision.PartitionId);
        Assert.Equal(2, decision.SourceShardId);
        Assert.Equal(3, decision.TargetShardId);
        Assert.Equal(new[] { RadarProcessingRebalanceSkippedReason.NoHotShard }, decision.SkippedReasons);
        Assert.Equal(new[] { RadarProcessingRebalancePolicyRejection.GlobalMoveBudgetExhausted }, decision.PolicyRejections);
    }

    [Fact]
    public void RecentDecisionRejectsInvalidShape()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRecentDecision(
                decisionId: -1,
                evaluationSequence: 0,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceDecisionKind.NoAction));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRecentDecision(
                decisionId: 0,
                evaluationSequence: -1,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceDecisionKind.NoAction));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRecentDecision(
                decisionId: 0,
                evaluationSequence: 0,
                RadarProcessingTopologyVersion.Initial,
                (RadarProcessingRebalanceDecisionKind)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRecentDecision(
                decisionId: 0,
                evaluationSequence: 0,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceDecisionKind.AcceptedMove,
                (RadarProcessingRebalanceMoveKind)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRecentDecision(
                decisionId: 0,
                evaluationSequence: 0,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceDecisionKind.AcceptedMove,
                partitionId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRecentDecision(
                decisionId: 0,
                evaluationSequence: 0,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceDecisionKind.NoAction,
                skippedReasons: new[] { RadarProcessingRebalanceSkippedReason.None }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRecentDecision(
                decisionId: 0,
                evaluationSequence: 0,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceDecisionKind.NoAction,
                policyRejections: new[] { RadarProcessingRebalancePolicyRejection.None }));
    }

    [Fact]
    public void RecentDecisionCanBeProjectedFromDecision()
    {
        var decision = RadarProcessingRebalanceDecision.NoAction(
            decisionId: 11,
            evaluationSequence: 12,
            RadarProcessingTopologyVersion.Initial,
            pressureWindowSampleCount: 3,
            new[] { RadarProcessingRebalanceSkippedReason.NoHotShard });

        var recent = RadarProcessingRebalanceRecentDecision.FromDecision(decision);

        Assert.Equal(decision.DecisionId, recent.DecisionId);
        Assert.Equal(decision.EvaluationSequence, recent.EvaluationSequence);
        Assert.Equal(decision.TopologyVersion, recent.TopologyVersion);
        Assert.Equal(decision.Kind, recent.Kind);
        Assert.Equal(RadarProcessingRebalanceMoveKind.None, recent.MoveKind);
        Assert.Null(recent.PartitionId);
        Assert.Equal(decision.SkippedReasons, recent.SkippedReasons);
    }

    [Fact]
    public void RecentAcceptedMoveCarriesProjectedPressure()
    {
        var pressure = CreateProjectedPressure();

        var move = new RadarProcessingRebalanceRecentAcceptedMove(
            decisionId: 1,
            evaluationSequence: 2,
            RadarProcessingTopologyVersion.Initial,
            new RadarProcessingTopologyVersion(2),
            RadarProcessingRebalanceMoveKind.DirectHotRelief,
            partitionId: 3,
            sourceShardId: 4,
            targetShardId: 5,
            pressure,
            expectedRelief: 6.5);

        Assert.Equal(1, move.DecisionId);
        Assert.Equal(2, move.EvaluationSequence);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, move.TopologyVersion);
        Assert.Equal(new RadarProcessingTopologyVersion(2), move.ResultTopologyVersion);
        Assert.Equal(RadarProcessingRebalanceMoveKind.DirectHotRelief, move.MoveKind);
        Assert.Equal(3, move.PartitionId);
        Assert.Equal(4, move.SourceShardId);
        Assert.Equal(5, move.TargetShardId);
        Assert.Equal(pressure, move.ProjectedPressure);
        Assert.Equal(6.5, move.ExpectedRelief);
    }

    [Fact]
    public void RecentAcceptedMoveRejectsInvalidShape()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAcceptedMove(decisionId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAcceptedMove(moveKind: RadarProcessingRebalanceMoveKind.None));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAcceptedMove(moveKind: (RadarProcessingRebalanceMoveKind)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAcceptedMove(partitionId: -1));
        Assert.Throws<ArgumentException>(() =>
            CreateAcceptedMove(sourceShardId: 1, targetShardId: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAcceptedMove(expectedRelief: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAcceptedMove(expectedRelief: -1.0));
    }

    [Fact]
    public void RecentAcceptedMoveCanBeProjectedFromAcceptedDecision()
    {
        var candidate = new RadarProcessingRebalanceCandidate(
            RadarProcessingRebalanceMoveKind.ColdEvacuation,
            partitionId: 1,
            sourceShardId: 2,
            targetShardId: 3,
            CreateProjectedPressure(),
            expectedRelief: 4.0);
        var decision = RadarProcessingRebalanceDecision.AcceptedMove(
            decisionId: 5,
            evaluationSequence: 6,
            RadarProcessingTopologyVersion.Initial,
            pressureWindowSampleCount: 7,
            candidate,
            resultTopologyVersion: new RadarProcessingTopologyVersion(2));

        var recent = RadarProcessingRebalanceRecentAcceptedMove.FromDecision(decision);

        Assert.Equal(decision.DecisionId, recent.DecisionId);
        Assert.Equal(decision.EvaluationSequence, recent.EvaluationSequence);
        Assert.Equal(decision.TopologyVersion, recent.TopologyVersion);
        Assert.Equal(decision.ResultTopologyVersion, recent.ResultTopologyVersion);
        Assert.Equal(candidate.MoveKind, recent.MoveKind);
        Assert.Equal(candidate.PartitionId, recent.PartitionId);
        Assert.Equal(candidate.ExpectedRelief, recent.ExpectedRelief);
    }

    [Fact]
    public void RecentAcceptedMoveRejectsNonAcceptedDecisionProjection()
    {
        var decision = RadarProcessingRebalanceDecision.NoAction(
            decisionId: 1,
            evaluationSequence: 1,
            RadarProcessingTopologyVersion.Initial,
            pressureWindowSampleCount: 1,
            new[] { RadarProcessingRebalanceSkippedReason.NoHotShard });

        Assert.Throws<ArgumentException>(() =>
            RadarProcessingRebalanceRecentAcceptedMove.FromDecision(decision));
    }

    [Fact]
    public void RecentValidationFailureCarriesErrorCodes()
    {
        var failure = new RadarProcessingRebalanceRecentValidationFailure(
            evaluationSequence: 9,
            RadarProcessingTopologyVersion.Initial,
            RadarProcessingRebalanceValidationError.MigrationFailed,
            RadarProcessingMigrationValidationError.StaleTopologyVersion,
            RadarProcessingStateHandoffValidationError.RawValueChecksumMismatch);

        Assert.Equal(9, failure.EvaluationSequence);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, failure.TopologyVersion);
        Assert.Equal(RadarProcessingRebalanceValidationError.MigrationFailed, failure.Error);
        Assert.Equal(RadarProcessingMigrationValidationError.StaleTopologyVersion, failure.MigrationError);
        Assert.Equal(RadarProcessingStateHandoffValidationError.RawValueChecksumMismatch, failure.HandoffError);
    }

    [Fact]
    public void RecentValidationFailureRejectsInvalidShape()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRecentValidationFailure(
                evaluationSequence: -1,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceValidationError.MigrationFailed));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRecentValidationFailure(
                evaluationSequence: 0,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceValidationError.None));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRecentValidationFailure(
                evaluationSequence: 0,
                RadarProcessingTopologyVersion.Initial,
                (RadarProcessingRebalanceValidationError)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRecentValidationFailure(
                evaluationSequence: 0,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceValidationError.MigrationFailed,
                migrationError: (RadarProcessingMigrationValidationError)255));
    }

    [Fact]
    public void RecentValidationFailureCanBeProjectedFromInvalidResult()
    {
        var validation = RadarProcessingRebalanceValidationResult.Invalid(
            RadarProcessingRebalanceValidationError.StateHandoffValidationFailed,
            "handoff failed",
            handoffError: RadarProcessingStateHandoffValidationError.ProcessingChecksumMismatch);

        var failure = RadarProcessingRebalanceRecentValidationFailure.FromResult(
            evaluationSequence: 3,
            RadarProcessingTopologyVersion.Initial,
            validation);

        Assert.Equal(3, failure.EvaluationSequence);
        Assert.Equal(validation.Error, failure.Error);
        Assert.Equal(validation.HandoffError, failure.HandoffError);
        Assert.Throws<ArgumentException>(() =>
            RadarProcessingRebalanceRecentValidationFailure.FromResult(
                evaluationSequence: 3,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceValidationResult.Valid()));
    }

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
            new RadarProcessingRebalanceRetentionStats(retainedDecisionCount: 1));

        skippedCounters.Add(new RadarProcessingRebalanceSkippedReasonCounter(
            RadarProcessingRebalanceSkippedReason.NoColdTargetShard,
            1));
        decisions.Clear();
        acceptedMoves.Clear();
        validationFailures.Clear();

        Assert.Equal(5, summary.Counters.EvaluationCount);
        Assert.Single(summary.SkippedReasonCounters);
        Assert.Single(summary.RecentDecisions);
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
        Assert.Empty(summary.RecentAcceptedMoves);
        Assert.Empty(summary.RecentValidationFailures);
        Assert.False(summary.RetentionStats.HasDroppedDetail);
    }

    private static RadarProcessingProjectedPressure CreateProjectedPressure() =>
        new(
            new RadarProcessingPressureScore(10),
            new RadarProcessingPressureScore(1),
            new RadarProcessingPressureScore(5),
            new RadarProcessingPressureScore(6));

    private static RadarProcessingRebalanceRecentAcceptedMove CreateAcceptedMove(
        long decisionId = 1,
        RadarProcessingRebalanceMoveKind moveKind = RadarProcessingRebalanceMoveKind.DirectHotRelief,
        int partitionId = 1,
        int sourceShardId = 0,
        int targetShardId = 1,
        double expectedRelief = 1.0) =>
        new(
            decisionId,
            evaluationSequence: 2,
            RadarProcessingTopologyVersion.Initial,
            resultTopologyVersion: null,
            moveKind,
            partitionId,
            sourceShardId,
            targetShardId,
            CreateProjectedPressure(),
            expectedRelief);
}
