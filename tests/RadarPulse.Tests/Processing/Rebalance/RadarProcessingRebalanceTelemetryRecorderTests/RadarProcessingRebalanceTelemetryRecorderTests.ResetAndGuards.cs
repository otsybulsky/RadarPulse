using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceTelemetryRecorderTests
{
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
        Assert.Empty(summary.RecentLifecycleTransitions);
        Assert.Empty(summary.RecentAcceptedMoves);
        Assert.Empty(summary.RecentValidationFailures);
        Assert.False(summary.RetentionStats.HasDroppedDetail);
    }

    [Fact]
    public void RecorderRejectsInvalidInput()
    {
        var recorder = new RadarProcessingRebalanceTelemetryRecorder();

        Assert.Throws<ArgumentNullException>(() => recorder.RecordDecision(null!));
        Assert.Throws<ArgumentNullException>(() => recorder.RecordQuarantineTransition(null!));
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
}
