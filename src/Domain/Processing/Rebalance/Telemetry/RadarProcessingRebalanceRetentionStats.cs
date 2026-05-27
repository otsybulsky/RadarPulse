namespace RadarPulse.Domain.Processing;

/// <summary>
/// Retention and drop counts for bounded rebalance telemetry detail windows.
/// </summary>
public sealed record RadarProcessingRebalanceRetentionStats
{
    /// <summary>
    /// Creates retention statistics for retained rebalance detail.
    /// </summary>
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

    /// <summary>
    /// Number of recent decision entries currently retained.
    /// </summary>
    public long RetainedDecisionCount { get; }

    /// <summary>
    /// Number of decision entries dropped because retention was full or disabled.
    /// </summary>
    public long DroppedDecisionCount { get; }

    /// <summary>
    /// Number of lifecycle transition entries currently retained.
    /// </summary>
    public long RetainedLifecycleTransitionCount { get; }

    /// <summary>
    /// Number of lifecycle transition entries dropped because retention was full or disabled.
    /// </summary>
    public long DroppedLifecycleTransitionCount { get; }

    /// <summary>
    /// Number of accepted move entries currently retained.
    /// </summary>
    public long RetainedAcceptedMoveCount { get; }

    /// <summary>
    /// Number of accepted move entries dropped because retention was full or disabled.
    /// </summary>
    public long DroppedAcceptedMoveCount { get; }

    /// <summary>
    /// Number of validation failure entries currently retained.
    /// </summary>
    public long RetainedValidationFailureCount { get; }

    /// <summary>
    /// Number of validation failure entries dropped because retention was full or disabled.
    /// </summary>
    public long DroppedValidationFailureCount { get; }

    /// <summary>
    /// Indicates whether any detail entry was dropped.
    /// </summary>
    public bool HasDroppedDetail =>
        DroppedDecisionCount > 0 ||
        DroppedLifecycleTransitionCount > 0 ||
        DroppedAcceptedMoveCount > 0 ||
        DroppedValidationFailureCount > 0;
}
