namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingRetainedPayloadTelemetrySummary
{
    public static RadarProcessingRetainedPayloadTelemetrySummary Empty { get; } = new();

    public RadarProcessingRetainedPayloadTelemetrySummary(
        RadarProcessingRetainedPayloadStrategy strategy = RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
        long retentionAttemptCount = 0,
        long retainedBatchCount = 0,
        long retentionUnsupportedStrategyCount = 0,
        long retentionFailedCopyCount = 0,
        long retentionCanceledCount = 0,
        long retentionInvalidInputCount = 0,
        long retainedEventCount = 0,
        long retainedPayloadBytes = 0,
        long retainedPayloadValueCount = 0,
        long allocatedBytes = 0,
        TimeSpan totalRetentionTime = default,
        long transferCount = 0,
        long poolRentCount = 0,
        long poolReturnCount = 0,
        long poolMissCount = 0,
        long releaseAttemptCount = 0,
        long releasedBatchCount = 0,
        long alreadyReleasedBatchCount = 0,
        long releaseFailedCount = 0,
        long releaseNotRequiredCount = 0,
        TimeSpan totalReleaseTime = default)
    {
        RadarProcessingRetainedPayloadOptions.EnsureKnownStrategy(strategy);
        ArgumentOutOfRangeException.ThrowIfNegative(retentionAttemptCount);
        ArgumentOutOfRangeException.ThrowIfNegative(retainedBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(retentionUnsupportedStrategyCount);
        ArgumentOutOfRangeException.ThrowIfNegative(retentionFailedCopyCount);
        ArgumentOutOfRangeException.ThrowIfNegative(retentionCanceledCount);
        ArgumentOutOfRangeException.ThrowIfNegative(retentionInvalidInputCount);
        ArgumentOutOfRangeException.ThrowIfNegative(retainedEventCount);
        ArgumentOutOfRangeException.ThrowIfNegative(retainedPayloadBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(retainedPayloadValueCount);
        ArgumentOutOfRangeException.ThrowIfNegative(allocatedBytes);
        EnsureNonNegative(totalRetentionTime, nameof(totalRetentionTime));
        ArgumentOutOfRangeException.ThrowIfNegative(transferCount);
        ArgumentOutOfRangeException.ThrowIfNegative(poolRentCount);
        ArgumentOutOfRangeException.ThrowIfNegative(poolReturnCount);
        ArgumentOutOfRangeException.ThrowIfNegative(poolMissCount);
        ArgumentOutOfRangeException.ThrowIfNegative(releaseAttemptCount);
        ArgumentOutOfRangeException.ThrowIfNegative(releasedBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(alreadyReleasedBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(releaseFailedCount);
        ArgumentOutOfRangeException.ThrowIfNegative(releaseNotRequiredCount);
        EnsureNonNegative(totalReleaseTime, nameof(totalReleaseTime));

        var retentionOutcomeCount = checked(
            retainedBatchCount +
            retentionUnsupportedStrategyCount +
            retentionFailedCopyCount +
            retentionCanceledCount +
            retentionInvalidInputCount);
        if (retentionOutcomeCount > retentionAttemptCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retainedBatchCount),
                retainedBatchCount,
                "Retention outcomes cannot exceed retention attempts.");
        }

        var releaseOutcomeCount = checked(
            releasedBatchCount +
            alreadyReleasedBatchCount +
            releaseFailedCount +
            releaseNotRequiredCount);
        if (releaseOutcomeCount > releaseAttemptCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(releasedBatchCount),
                releasedBatchCount,
                "Release outcomes cannot exceed release attempts.");
        }

        Strategy = strategy;
        RetentionAttemptCount = retentionAttemptCount;
        RetainedBatchCount = retainedBatchCount;
        RetentionUnsupportedStrategyCount = retentionUnsupportedStrategyCount;
        RetentionFailedCopyCount = retentionFailedCopyCount;
        RetentionCanceledCount = retentionCanceledCount;
        RetentionInvalidInputCount = retentionInvalidInputCount;
        RetainedEventCount = retainedEventCount;
        RetainedPayloadBytes = retainedPayloadBytes;
        RetainedPayloadValueCount = retainedPayloadValueCount;
        AllocatedBytes = allocatedBytes;
        TotalRetentionTime = totalRetentionTime;
        TransferCount = transferCount;
        PoolRentCount = poolRentCount;
        PoolReturnCount = poolReturnCount;
        PoolMissCount = poolMissCount;
        ReleaseAttemptCount = releaseAttemptCount;
        ReleasedBatchCount = releasedBatchCount;
        AlreadyReleasedBatchCount = alreadyReleasedBatchCount;
        ReleaseFailedCount = releaseFailedCount;
        ReleaseNotRequiredCount = releaseNotRequiredCount;
        TotalReleaseTime = totalReleaseTime;
    }

    public RadarProcessingRetainedPayloadStrategy Strategy { get; }

    public long RetentionAttemptCount { get; }

    public long RetainedBatchCount { get; }

    public long RetentionUnsupportedStrategyCount { get; }

    public long RetentionFailedCopyCount { get; }

    public long RetentionCanceledCount { get; }

    public long RetentionInvalidInputCount { get; }

    public long RetainedEventCount { get; }

    public long RetainedPayloadBytes { get; }

    public long RetainedPayloadValueCount { get; }

    public long AllocatedBytes { get; }

    public TimeSpan TotalRetentionTime { get; }

    public long TransferCount { get; }

    public long PoolRentCount { get; }

    public long PoolReturnCount { get; }

    public long PoolMissCount { get; }

    public long ReleaseAttemptCount { get; }

    public long ReleasedBatchCount { get; }

    public long AlreadyReleasedBatchCount { get; }

    public long ReleaseFailedCount { get; }

    public long ReleaseNotRequiredCount { get; }

    public TimeSpan TotalReleaseTime { get; }

    public long FailedRetentionCount =>
        RetentionUnsupportedStrategyCount +
        RetentionFailedCopyCount +
        RetentionCanceledCount +
        RetentionInvalidInputCount;

    public bool HasFailures =>
        FailedRetentionCount > 0 ||
        ReleaseFailedCount > 0;

    public double AllocatedBytesPerRetainedBatch =>
        RetainedBatchCount == 0 ? 0 : (double)AllocatedBytes / RetainedBatchCount;

    public double AllocatedBytesPerPayloadValue =>
        RetainedPayloadValueCount == 0 ? 0 : (double)AllocatedBytes / RetainedPayloadValueCount;

    private static void EnsureNonNegative(
        TimeSpan value,
        string paramName)
    {
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }
    }
}
