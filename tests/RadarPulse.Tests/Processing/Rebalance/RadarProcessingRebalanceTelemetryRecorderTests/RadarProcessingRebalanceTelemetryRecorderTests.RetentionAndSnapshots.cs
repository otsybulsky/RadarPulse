using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceTelemetryRecorderTests
{
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
        Assert.Equal(0, summary.RetentionStats.DroppedLifecycleTransitionCount);
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
        recorder.RecordQuarantineTransition(CreateQuarantineTransition(RadarProcessingQuarantineTransitionReason.EnteredQuarantine));
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
        Assert.Empty(summary.RecentLifecycleTransitions);
        Assert.Empty(summary.RecentAcceptedMoves);
        Assert.Empty(summary.RecentValidationFailures);
        Assert.Equal(1, summary.RetentionStats.DroppedDecisionCount);
        Assert.Equal(1, summary.RetentionStats.DroppedLifecycleTransitionCount);
        Assert.Equal(1, summary.RetentionStats.DroppedAcceptedMoveCount);
        Assert.Equal(1, summary.RetentionStats.DroppedValidationFailureCount);
    }
}
