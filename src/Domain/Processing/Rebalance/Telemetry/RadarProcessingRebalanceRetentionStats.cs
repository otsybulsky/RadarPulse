namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingRebalanceRetentionStats
{
    public RadarProcessingRebalanceRetentionStats(
        long retainedDecisionCount = 0,
        long droppedDecisionCount = 0,
        long retainedLifecycleTransitionCount = 0,
        long droppedLifecycleTransitionCount = 0,
        long retainedAcceptedMoveCount = 0,
        long droppedAcceptedMoveCount = 0,
        long retainedValidationFailureCount = 0,
        long droppedValidationFailureCount = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(retainedDecisionCount);
        ArgumentOutOfRangeException.ThrowIfNegative(droppedDecisionCount);
        ArgumentOutOfRangeException.ThrowIfNegative(retainedLifecycleTransitionCount);
        ArgumentOutOfRangeException.ThrowIfNegative(droppedLifecycleTransitionCount);
        ArgumentOutOfRangeException.ThrowIfNegative(retainedAcceptedMoveCount);
        ArgumentOutOfRangeException.ThrowIfNegative(droppedAcceptedMoveCount);
        ArgumentOutOfRangeException.ThrowIfNegative(retainedValidationFailureCount);
        ArgumentOutOfRangeException.ThrowIfNegative(droppedValidationFailureCount);

        RetainedDecisionCount = retainedDecisionCount;
        DroppedDecisionCount = droppedDecisionCount;
        RetainedLifecycleTransitionCount = retainedLifecycleTransitionCount;
        DroppedLifecycleTransitionCount = droppedLifecycleTransitionCount;
        RetainedAcceptedMoveCount = retainedAcceptedMoveCount;
        DroppedAcceptedMoveCount = droppedAcceptedMoveCount;
        RetainedValidationFailureCount = retainedValidationFailureCount;
        DroppedValidationFailureCount = droppedValidationFailureCount;
    }

    public long RetainedDecisionCount { get; }

    public long DroppedDecisionCount { get; }

    public long RetainedLifecycleTransitionCount { get; }

    public long DroppedLifecycleTransitionCount { get; }

    public long RetainedAcceptedMoveCount { get; }

    public long DroppedAcceptedMoveCount { get; }

    public long RetainedValidationFailureCount { get; }

    public long DroppedValidationFailureCount { get; }

    public bool HasDroppedDetail =>
        DroppedDecisionCount > 0 ||
        DroppedLifecycleTransitionCount > 0 ||
        DroppedAcceptedMoveCount > 0 ||
        DroppedValidationFailureCount > 0;
}
