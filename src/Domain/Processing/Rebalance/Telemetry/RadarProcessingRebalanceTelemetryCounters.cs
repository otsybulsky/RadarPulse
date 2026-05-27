namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingRebalanceTelemetryCounters
{
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

    public long EvaluationCount { get; }

    public long NoActionDecisionCount { get; }

    public long AcceptedMoveCount { get; }

    public long RejectedCandidateCount { get; }

    public long DirectHotReliefMoveCount { get; }

    public long ColdEvacuationMoveCount { get; }

    public long FailedMigrationCount { get; }

    public long ValidationFailureCount { get; }

    public long QuarantineEntryCount { get; }

    public long QuarantineClearCount { get; }

    public long QuarantineRetryCount { get; }

    public long QuarantineReentryCount { get; }
}
