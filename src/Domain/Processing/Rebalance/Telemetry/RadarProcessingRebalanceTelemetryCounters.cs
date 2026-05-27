namespace RadarPulse.Domain.Processing;

/// <summary>
/// Aggregate rebalance telemetry counters retained across evaluations.
/// </summary>
public sealed record RadarProcessingRebalanceTelemetryCounters
{
    /// <summary>
    /// Creates a rebalance counter snapshot.
    /// </summary>
    public RadarProcessingRebalanceTelemetryCounters(
        long evaluationCount = 0,
        long noActionDecisionCount = 0,
        long acceptedMoveCount = 0,
        long rejectedCandidateCount = 0,
        long directHotReliefMoveCount = 0,
        long coldEvacuationMoveCount = 0,
        long failedMigrationCount = 0,
        long validationFailureCount = 0,
        long quarantineEntryCount = 0,
        long quarantineClearCount = 0,
        long quarantineRetryCount = 0,
        long quarantineReentryCount = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(evaluationCount);
        ArgumentOutOfRangeException.ThrowIfNegative(noActionDecisionCount);
        ArgumentOutOfRangeException.ThrowIfNegative(acceptedMoveCount);
        ArgumentOutOfRangeException.ThrowIfNegative(rejectedCandidateCount);
        ArgumentOutOfRangeException.ThrowIfNegative(directHotReliefMoveCount);
        ArgumentOutOfRangeException.ThrowIfNegative(coldEvacuationMoveCount);
        ArgumentOutOfRangeException.ThrowIfNegative(failedMigrationCount);
        ArgumentOutOfRangeException.ThrowIfNegative(validationFailureCount);
        ArgumentOutOfRangeException.ThrowIfNegative(quarantineEntryCount);
        ArgumentOutOfRangeException.ThrowIfNegative(quarantineClearCount);
        ArgumentOutOfRangeException.ThrowIfNegative(quarantineRetryCount);
        ArgumentOutOfRangeException.ThrowIfNegative(quarantineReentryCount);

        if (directHotReliefMoveCount + coldEvacuationMoveCount > acceptedMoveCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(acceptedMoveCount),
                acceptedMoveCount,
                "Accepted move count must cover counted concrete move kinds.");
        }

        EvaluationCount = evaluationCount;
        NoActionDecisionCount = noActionDecisionCount;
        AcceptedMoveCount = acceptedMoveCount;
        RejectedCandidateCount = rejectedCandidateCount;
        DirectHotReliefMoveCount = directHotReliefMoveCount;
        ColdEvacuationMoveCount = coldEvacuationMoveCount;
        FailedMigrationCount = failedMigrationCount;
        ValidationFailureCount = validationFailureCount;
        QuarantineEntryCount = quarantineEntryCount;
        QuarantineClearCount = quarantineClearCount;
        QuarantineRetryCount = quarantineRetryCount;
        QuarantineReentryCount = quarantineReentryCount;
    }

    /// <summary>
    /// Number of planner decisions recorded as evaluations.
    /// </summary>
    public long EvaluationCount { get; }

    /// <summary>
    /// Number of no-action decisions.
    /// </summary>
    public long NoActionDecisionCount { get; }

    /// <summary>
    /// Number of accepted move decisions.
    /// </summary>
    public long AcceptedMoveCount { get; }

    /// <summary>
    /// Number of rejected candidate decisions.
    /// </summary>
    public long RejectedCandidateCount { get; }

    /// <summary>
    /// Number of accepted direct hot-relief moves.
    /// </summary>
    public long DirectHotReliefMoveCount { get; }

    /// <summary>
    /// Number of accepted cold-evacuation moves.
    /// </summary>
    public long ColdEvacuationMoveCount { get; }

    /// <summary>
    /// Number of accepted decisions whose migration failed.
    /// </summary>
    public long FailedMigrationCount { get; }

    /// <summary>
    /// Number of invalid rebalance validation results.
    /// </summary>
    public long ValidationFailureCount { get; }

    /// <summary>
    /// Number of quarantine entry transitions.
    /// </summary>
    public long QuarantineEntryCount { get; }

    /// <summary>
    /// Number of quarantine clear transitions.
    /// </summary>
    public long QuarantineClearCount { get; }

    /// <summary>
    /// Number of transitions that made a quarantined partition retry-eligible.
    /// </summary>
    public long QuarantineRetryCount { get; }

    /// <summary>
    /// Number of quarantine reentry transitions.
    /// </summary>
    public long QuarantineReentryCount { get; }
}
