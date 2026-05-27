namespace RadarPulse.Domain.Processing;

/// <summary>
/// Aggregate telemetry for retained payload retention and release operations.
/// </summary>
public sealed record RadarProcessingRetainedPayloadTelemetrySummary
{
    /// <summary>
    /// Empty retained payload telemetry summary.
    /// </summary>
    public static RadarProcessingRetainedPayloadTelemetrySummary Empty { get; } = new();

    /// <summary>
    /// Creates retained payload telemetry.
    /// </summary>
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
        TimeSpan totalReleaseTime = default,
        long eventPoolRentCount = 0,
        long eventPoolReturnCount = 0,
        long eventPoolMissCount = 0,
        long payloadPoolRentCount = 0,
        long payloadPoolReturnCount = 0,
        long payloadPoolMissCount = 0)
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
        ArgumentOutOfRangeException.ThrowIfNegative(eventPoolRentCount);
        ArgumentOutOfRangeException.ThrowIfNegative(eventPoolReturnCount);
        ArgumentOutOfRangeException.ThrowIfNegative(eventPoolMissCount);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadPoolRentCount);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadPoolReturnCount);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadPoolMissCount);

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
        EventPoolRentCount = eventPoolRentCount;
        EventPoolReturnCount = eventPoolReturnCount;
        EventPoolMissCount = eventPoolMissCount;
        PayloadPoolRentCount = payloadPoolRentCount;
        PayloadPoolReturnCount = payloadPoolReturnCount;
        PayloadPoolMissCount = payloadPoolMissCount;
    }

    /// <summary>
    /// Retention strategy represented by the telemetry.
    /// </summary>
    public RadarProcessingRetainedPayloadStrategy Strategy { get; }

    /// <summary>
    /// Number of retention attempts.
    /// </summary>
    public long RetentionAttemptCount { get; }

    /// <summary>
    /// Number of successfully retained batches.
    /// </summary>
    public long RetainedBatchCount { get; }

    /// <summary>
    /// Number of retention attempts rejected for unsupported strategy.
    /// </summary>
    public long RetentionUnsupportedStrategyCount { get; }

    /// <summary>
    /// Number of retention attempts that failed during copy.
    /// </summary>
    public long RetentionFailedCopyCount { get; }

    /// <summary>
    /// Number of canceled retention attempts.
    /// </summary>
    public long RetentionCanceledCount { get; }

    /// <summary>
    /// Number of retention attempts rejected for invalid input.
    /// </summary>
    public long RetentionInvalidInputCount { get; }

    /// <summary>
    /// Total event count retained successfully.
    /// </summary>
    public long RetainedEventCount { get; }

    /// <summary>
    /// Total retained payload bytes.
    /// </summary>
    public long RetainedPayloadBytes { get; }

    /// <summary>
    /// Total retained payload value count.
    /// </summary>
    public long RetainedPayloadValueCount { get; }

    /// <summary>
    /// Bytes allocated during retention.
    /// </summary>
    public long AllocatedBytes { get; }

    /// <summary>
    /// Total time spent retaining payloads.
    /// </summary>
    public TimeSpan TotalRetentionTime { get; }

    /// <summary>
    /// Number of builder transfer operations.
    /// </summary>
    public long TransferCount { get; }

    /// <summary>
    /// Total pool rent count.
    /// </summary>
    public long PoolRentCount { get; }

    /// <summary>
    /// Total pool return count.
    /// </summary>
    public long PoolReturnCount { get; }

    /// <summary>
    /// Total pool miss count.
    /// </summary>
    public long PoolMissCount { get; }

    /// <summary>
    /// Number of release attempts.
    /// </summary>
    public long ReleaseAttemptCount { get; }

    /// <summary>
    /// Number of released batches.
    /// </summary>
    public long ReleasedBatchCount { get; }

    /// <summary>
    /// Number of already-released results.
    /// </summary>
    public long AlreadyReleasedBatchCount { get; }

    /// <summary>
    /// Number of failed releases.
    /// </summary>
    public long ReleaseFailedCount { get; }

    /// <summary>
    /// Number of releases that were not required.
    /// </summary>
    public long ReleaseNotRequiredCount { get; }

    /// <summary>
    /// Total time spent releasing retained payloads.
    /// </summary>
    public TimeSpan TotalReleaseTime { get; }

    /// <summary>
    /// Event buffer pool rent count.
    /// </summary>
    public long EventPoolRentCount { get; }

    /// <summary>
    /// Event buffer pool return count.
    /// </summary>
    public long EventPoolReturnCount { get; }

    /// <summary>
    /// Event buffer pool miss count.
    /// </summary>
    public long EventPoolMissCount { get; }

    /// <summary>
    /// Payload buffer pool rent count.
    /// </summary>
    public long PayloadPoolRentCount { get; }

    /// <summary>
    /// Payload buffer pool return count.
    /// </summary>
    public long PayloadPoolReturnCount { get; }

    /// <summary>
    /// Payload buffer pool miss count.
    /// </summary>
    public long PayloadPoolMissCount { get; }

    /// <summary>
    /// Total failed retention attempts.
    /// </summary>
    public long FailedRetentionCount =>
        RetentionUnsupportedStrategyCount +
        RetentionFailedCopyCount +
        RetentionCanceledCount +
        RetentionInvalidInputCount;

    /// <summary>
    /// Indicates whether retention or release failures were recorded.
    /// </summary>
    public bool HasFailures =>
        FailedRetentionCount > 0 ||
        ReleaseFailedCount > 0;

    /// <summary>
    /// Allocated bytes per successfully retained batch.
    /// </summary>
    public double AllocatedBytesPerRetainedBatch =>
        RetainedBatchCount == 0 ? 0 : (double)AllocatedBytes / RetainedBatchCount;

    /// <summary>
    /// Allocated bytes per retained payload value.
    /// </summary>
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
