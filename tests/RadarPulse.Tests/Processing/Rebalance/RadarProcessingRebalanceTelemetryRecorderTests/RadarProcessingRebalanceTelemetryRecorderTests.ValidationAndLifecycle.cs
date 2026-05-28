using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceTelemetryRecorderTests
{
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

        recorder.RecordQuarantineTransition(
            CreateQuarantineTransition(RadarProcessingQuarantineTransitionReason.EnteredQuarantine));
        recorder.RecordQuarantineTransition(
            CreateQuarantineTransition(RadarProcessingQuarantineTransitionReason.ClearedBySustainedCooling));
        recorder.RecordQuarantineTransition(
            CreateQuarantineTransition(RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl));
        recorder.RecordQuarantineTransition(
            CreateQuarantineTransition(RadarProcessingQuarantineTransitionReason.ReenteredQuarantine));

        var summary = recorder.CreateSummary();

        Assert.Equal(1, summary.Counters.QuarantineEntryCount);
        Assert.Equal(1, summary.Counters.QuarantineClearCount);
        Assert.Equal(1, summary.Counters.QuarantineRetryCount);
        Assert.Equal(1, summary.Counters.QuarantineReentryCount);
        Assert.Equal(4, summary.RecentLifecycleTransitions.Count);
        Assert.Equal(RadarProcessingQuarantineTransitionReason.EnteredQuarantine, summary.RecentLifecycleTransitions[0].Reason);
    }

    [Fact]
    public void RecorderBoundsRecentLifecycleTransitions()
    {
        var recorder = new RadarProcessingRebalanceTelemetryRecorder(
            new RadarProcessingTelemetryRetentionOptions(
                RadarProcessingDiagnosticRetentionMode.Recent,
                maxRetainedDecisions: 0,
                maxRetainedLifecycleTransitions: 2,
                maxRetainedAcceptedMoves: 0,
                maxRetainedValidationFailures: 0));

        recorder.RecordQuarantineTransition(
            CreateQuarantineTransition(
                RadarProcessingQuarantineTransitionReason.EnteredQuarantine,
                evaluationSequence: 1));
        recorder.RecordQuarantineTransition(
            CreateQuarantineTransition(
                RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl,
                evaluationSequence: 2));
        recorder.RecordQuarantineTransition(
            CreateQuarantineTransition(
                RadarProcessingQuarantineTransitionReason.ReenteredQuarantine,
                evaluationSequence: 3));

        var summary = recorder.CreateSummary();

        Assert.Equal(1, summary.Counters.QuarantineEntryCount);
        Assert.Equal(1, summary.Counters.QuarantineRetryCount);
        Assert.Equal(1, summary.Counters.QuarantineReentryCount);
        Assert.Equal(new long[] { 2, 3 }, summary.RecentLifecycleTransitions.Select(static transition => transition.EvaluationSequence));
        Assert.Equal(2, summary.RetentionStats.RetainedLifecycleTransitionCount);
        Assert.Equal(1, summary.RetentionStats.DroppedLifecycleTransitionCount);
    }
}
