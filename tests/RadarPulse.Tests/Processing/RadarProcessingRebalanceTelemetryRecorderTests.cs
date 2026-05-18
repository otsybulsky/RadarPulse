using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingRebalanceTelemetryRecorderTests
{
    [Fact]
    public void BoundedWindowKeepsLatestItemsAndCountsDroppedItems()
    {
        var window = new RadarProcessingBoundedTelemetryWindow<string>(capacity: 2);

        window.Add("first");
        window.Add("second");
        var firstSnapshot = window.Snapshot();
        window.Add("third");

        Assert.Equal(2, window.Count);
        Assert.Equal(1, window.DroppedCount);
        Assert.Equal(new[] { "first", "second" }, firstSnapshot);
        Assert.Equal(new[] { "second", "third" }, window.Snapshot());
    }

    [Fact]
    public void BoundedWindowSupportsCountersOnlyCapacity()
    {
        var window = new RadarProcessingBoundedTelemetryWindow<string>(capacity: 0);

        window.Add("first");
        window.Add("second");

        Assert.Equal(0, window.Count);
        Assert.Equal(2, window.DroppedCount);
        Assert.Empty(window.Snapshot());
    }

    [Fact]
    public void BoundedWindowRejectsInvalidInputAndCanReset()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingBoundedTelemetryWindow<string>(capacity: -1));

        var window = new RadarProcessingBoundedTelemetryWindow<string>(capacity: 1);

        Assert.Throws<ArgumentNullException>(() => window.Add(null!));

        window.Add("first");
        window.Add("second");
        window.Clear();

        Assert.Equal(0, window.Count);
        Assert.Equal(0, window.DroppedCount);
        Assert.Empty(window.Snapshot());
    }

    [Fact]
    public void RecorderAggregatesNoActionDecisionAndSkippedReasons()
    {
        var recorder = new RadarProcessingRebalanceTelemetryRecorder();
        var decision = RadarProcessingRebalanceDecision.NoAction(
            decisionId: 1,
            evaluationSequence: 2,
            RadarProcessingTopologyVersion.Initial,
            pressureWindowSampleCount: 3,
            new[]
            {
                RadarProcessingRebalanceSkippedReason.NoHotShard,
                RadarProcessingRebalanceSkippedReason.NoColdTargetShard
            });

        recorder.RecordDecision(decision);
        var summary = recorder.CreateSummary();

        Assert.Equal(1, summary.Counters.EvaluationCount);
        Assert.Equal(1, summary.Counters.NoActionDecisionCount);
        Assert.Equal(0, summary.Counters.AcceptedMoveCount);
        Assert.Equal(2, summary.SkippedReasonCounters.Count);
        Assert.Equal(RadarProcessingRebalanceSkippedReason.NoHotShard, summary.SkippedReasonCounters[0].Reason);
        Assert.Equal(1, summary.SkippedReasonCounters[0].Count);
        Assert.Equal(RadarProcessingRebalanceSkippedReason.NoColdTargetShard, summary.SkippedReasonCounters[1].Reason);
        Assert.Single(summary.RecentDecisions);
        Assert.Equal(decision.DecisionId, summary.RecentDecisions[0].DecisionId);
        Assert.Empty(summary.RecentAcceptedMoves);
    }

    [Fact]
    public void RecorderAggregatesAcceptedMoveKindsAndRecentMoves()
    {
        var recorder = new RadarProcessingRebalanceTelemetryRecorder();
        var direct = CreateAcceptedDecision(
            decisionId: 1,
            RadarProcessingRebalanceMoveKind.DirectHotRelief);
        var cold = CreateAcceptedDecision(
            decisionId: 2,
            RadarProcessingRebalanceMoveKind.ColdEvacuation);

        recorder.RecordDecision(direct);
        recorder.RecordDecision(cold);
        var summary = recorder.CreateSummary();

        Assert.Equal(2, summary.Counters.EvaluationCount);
        Assert.Equal(2, summary.Counters.AcceptedMoveCount);
        Assert.Equal(1, summary.Counters.DirectHotReliefMoveCount);
        Assert.Equal(1, summary.Counters.ColdEvacuationMoveCount);
        Assert.Equal(2, summary.RecentDecisions.Count);
        Assert.Equal(2, summary.RecentAcceptedMoves.Count);
        Assert.Equal(RadarProcessingRebalanceMoveKind.DirectHotRelief, summary.RecentAcceptedMoves[0].MoveKind);
        Assert.Equal(RadarProcessingRebalanceMoveKind.ColdEvacuation, summary.RecentAcceptedMoves[1].MoveKind);
    }

    [Fact]
    public void RecorderAggregatesRejectedCandidateAndPolicySkippedReason()
    {
        var recorder = new RadarProcessingRebalanceTelemetryRecorder();
        var candidate = CreateCandidate();
        var decision = RadarProcessingRebalanceDecision.RejectedCandidate(
            decisionId: 3,
            evaluationSequence: 4,
            RadarProcessingTopologyVersion.Initial,
            pressureWindowSampleCount: 5,
            candidate,
            new[] { RadarProcessingRebalanceSkippedReason.TargetHeadroomExceeded },
            new[] { RadarProcessingRebalancePolicyRejection.TargetHeadroomExceeded });

        recorder.RecordDecision(decision);
        var summary = recorder.CreateSummary();

        Assert.Equal(1, summary.Counters.EvaluationCount);
        Assert.Equal(1, summary.Counters.RejectedCandidateCount);
        Assert.Equal(0, summary.Counters.AcceptedMoveCount);
        Assert.Single(summary.SkippedReasonCounters);
        Assert.Equal(RadarProcessingRebalanceSkippedReason.TargetHeadroomExceeded, summary.SkippedReasonCounters[0].Reason);
        Assert.Single(summary.RecentDecisions);
        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, summary.RecentDecisions[0].Kind);
        Assert.Equal(RadarProcessingRebalancePolicyRejection.TargetHeadroomExceeded, summary.RecentDecisions[0].PolicyRejections[0]);
    }

    [Fact]
    public void RecorderAppliesBoundedRecentDetailWindows()
    {
        var recorder = new RadarProcessingRebalanceTelemetryRecorder(
            new RadarProcessingTelemetryRetentionOptions(
                RadarProcessingDiagnosticRetentionMode.Recent,
                maxRetainedDecisions: 2,
                maxRetainedLifecycleTransitions: 0,
                maxRetainedAcceptedMoves: 1,
                maxRetainedValidationFailures: 1));

        recorder.RecordDecision(CreateNoActionDecision(1));
        recorder.RecordDecision(CreateNoActionDecision(2));
        recorder.RecordDecision(CreateAcceptedDecision(3, RadarProcessingRebalanceMoveKind.DirectHotRelief));
        recorder.RecordValidationResult(
            evaluationSequence: 1,
            RadarProcessingTopologyVersion.Initial,
            RadarProcessingRebalanceValidationResult.Invalid(
                RadarProcessingRebalanceValidationError.MigrationFailed,
                "first"));
        recorder.RecordValidationResult(
            evaluationSequence: 2,
            RadarProcessingTopologyVersion.Initial,
            RadarProcessingRebalanceValidationResult.Invalid(
                RadarProcessingRebalanceValidationError.StateHandoffValidationFailed,
                "second"));

        var summary = recorder.CreateSummary();

        Assert.Equal(3, summary.Counters.EvaluationCount);
        Assert.Equal(new long[] { 2, 3 }, summary.RecentDecisions.Select(static decision => decision.DecisionId));
        Assert.Single(summary.RecentAcceptedMoves);
        Assert.Equal(3, summary.RecentAcceptedMoves[0].DecisionId);
        Assert.Single(summary.RecentValidationFailures);
        Assert.Equal(RadarProcessingRebalanceValidationError.StateHandoffValidationFailed, summary.RecentValidationFailures[0].Error);
        Assert.Equal(1, summary.RetentionStats.DroppedDecisionCount);
        Assert.Equal(0, summary.RetentionStats.DroppedAcceptedMoveCount);
        Assert.Equal(1, summary.RetentionStats.DroppedValidationFailureCount);
        Assert.True(summary.RetentionStats.HasDroppedDetail);
    }

    [Fact]
    public void RecorderCountersOnlyModeDropsAllRecentDetail()
    {
        var recorder = new RadarProcessingRebalanceTelemetryRecorder(
            new RadarProcessingTelemetryRetentionOptions(
                RadarProcessingDiagnosticRetentionMode.Counters,
                maxRetainedDecisions: 10,
                maxRetainedLifecycleTransitions: 10,
                maxRetainedAcceptedMoves: 10,
                maxRetainedValidationFailures: 10));

        recorder.RecordDecision(CreateAcceptedDecision(1, RadarProcessingRebalanceMoveKind.DirectHotRelief));
        recorder.RecordValidationResult(
            evaluationSequence: 1,
            RadarProcessingTopologyVersion.Initial,
            RadarProcessingRebalanceValidationResult.Invalid(
                RadarProcessingRebalanceValidationError.MigrationFailed,
                "failed"));

        var summary = recorder.CreateSummary();

        Assert.Equal(1, summary.Counters.EvaluationCount);
        Assert.Equal(1, summary.Counters.AcceptedMoveCount);
        Assert.Equal(1, summary.Counters.ValidationFailureCount);
        Assert.Empty(summary.RecentDecisions);
        Assert.Empty(summary.RecentAcceptedMoves);
        Assert.Empty(summary.RecentValidationFailures);
        Assert.Equal(1, summary.RetentionStats.DroppedDecisionCount);
        Assert.Equal(1, summary.RetentionStats.DroppedAcceptedMoveCount);
        Assert.Equal(1, summary.RetentionStats.DroppedValidationFailureCount);
    }

    [Fact]
    public void RecorderSummarySnapshotIsStableAfterLaterMutations()
    {
        var recorder = new RadarProcessingRebalanceTelemetryRecorder();

        recorder.RecordDecision(CreateNoActionDecision(1));
        var first = recorder.CreateSummary();

        recorder.RecordDecision(CreateNoActionDecision(2));
        var second = recorder.CreateSummary();

        Assert.Equal(1, first.Counters.EvaluationCount);
        Assert.Single(first.RecentDecisions);
        Assert.Equal(1, first.RecentDecisions[0].DecisionId);
        Assert.Equal(2, second.Counters.EvaluationCount);
        Assert.Equal(2, second.RecentDecisions.Count);
    }

    [Fact]
    public void RecorderValidationFailureAggregationIgnoresValidResults()
    {
        var recorder = new RadarProcessingRebalanceTelemetryRecorder();

        recorder.RecordValidationResult(
            evaluationSequence: 1,
            RadarProcessingTopologyVersion.Initial,
            RadarProcessingRebalanceValidationResult.Valid());
        recorder.RecordValidationResult(
            evaluationSequence: 2,
            RadarProcessingTopologyVersion.Initial,
            RadarProcessingRebalanceValidationResult.Invalid(
                RadarProcessingRebalanceValidationError.MigrationFailed,
                "migration failed",
                migrationError: RadarProcessingMigrationValidationError.StaleTopologyVersion));

        var summary = recorder.CreateSummary();

        Assert.Equal(0, summary.Counters.EvaluationCount);
        Assert.Equal(1, summary.Counters.ValidationFailureCount);
        Assert.Equal(1, summary.Counters.FailedMigrationCount);
        Assert.Single(summary.RecentValidationFailures);
        Assert.Equal(2, summary.RecentValidationFailures[0].EvaluationSequence);
        Assert.Equal(RadarProcessingMigrationValidationError.StaleTopologyVersion, summary.RecentValidationFailures[0].MigrationError);
    }

    [Fact]
    public void RecorderTracksQuarantineLifecycleCounters()
    {
        var recorder = new RadarProcessingRebalanceTelemetryRecorder();

        recorder.RecordQuarantineEntry();
        recorder.RecordQuarantineClear();
        recorder.RecordQuarantineRetry();
        recorder.RecordQuarantineReentry();

        var summary = recorder.CreateSummary();

        Assert.Equal(1, summary.Counters.QuarantineEntryCount);
        Assert.Equal(1, summary.Counters.QuarantineClearCount);
        Assert.Equal(1, summary.Counters.QuarantineRetryCount);
        Assert.Equal(1, summary.Counters.QuarantineReentryCount);
    }

    [Fact]
    public void RecorderResetClearsCountersAndWindows()
    {
        var recorder = new RadarProcessingRebalanceTelemetryRecorder();

        recorder.RecordDecision(CreateAcceptedDecision(1, RadarProcessingRebalanceMoveKind.DirectHotRelief));
        recorder.RecordValidationResult(
            evaluationSequence: 1,
            RadarProcessingTopologyVersion.Initial,
            RadarProcessingRebalanceValidationResult.Invalid(
                RadarProcessingRebalanceValidationError.MigrationFailed,
                "failed"));

        recorder.Reset();
        var summary = recorder.CreateSummary();

        Assert.Equal(0, summary.Counters.EvaluationCount);
        Assert.Equal(0, summary.Counters.AcceptedMoveCount);
        Assert.Equal(0, summary.Counters.ValidationFailureCount);
        Assert.Empty(summary.SkippedReasonCounters);
        Assert.Empty(summary.RecentDecisions);
        Assert.Empty(summary.RecentAcceptedMoves);
        Assert.Empty(summary.RecentValidationFailures);
        Assert.False(summary.RetentionStats.HasDroppedDetail);
    }

    [Fact]
    public void RecorderRejectsInvalidInput()
    {
        var recorder = new RadarProcessingRebalanceTelemetryRecorder();

        Assert.Throws<ArgumentNullException>(() => recorder.RecordDecision(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            recorder.RecordValidationResult(
                evaluationSequence: -1,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceValidationResult.Valid()));
        Assert.Throws<ArgumentNullException>(() =>
            recorder.RecordValidationResult(
                evaluationSequence: 0,
                RadarProcessingTopologyVersion.Initial,
                null!));
    }

    private static RadarProcessingRebalanceDecision CreateNoActionDecision(
        long decisionId) =>
        RadarProcessingRebalanceDecision.NoAction(
            decisionId,
            evaluationSequence: decisionId,
            RadarProcessingTopologyVersion.Initial,
            pressureWindowSampleCount: 3,
            new[] { RadarProcessingRebalanceSkippedReason.NoHotShard });

    private static RadarProcessingRebalanceDecision CreateAcceptedDecision(
        long decisionId,
        RadarProcessingRebalanceMoveKind moveKind) =>
        RadarProcessingRebalanceDecision.AcceptedMove(
            decisionId,
            evaluationSequence: decisionId,
            RadarProcessingTopologyVersion.Initial,
            pressureWindowSampleCount: 3,
            CreateCandidate(moveKind));

    private static RadarProcessingRebalanceCandidate CreateCandidate(
        RadarProcessingRebalanceMoveKind moveKind = RadarProcessingRebalanceMoveKind.DirectHotRelief) =>
        new(
            moveKind,
            partitionId: 1,
            sourceShardId: 0,
            targetShardId: 1,
            new RadarProcessingProjectedPressure(
                new RadarProcessingPressureScore(10),
                new RadarProcessingPressureScore(1),
                new RadarProcessingPressureScore(5),
                new RadarProcessingPressureScore(6)),
            expectedRelief: 5);
}
