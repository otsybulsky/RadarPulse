namespace RadarPulse.Domain.Processing;

/// <summary>
/// Acceptance thresholds used by queued-provider rollout readiness gates.
/// </summary>
/// <remarks>
/// The thresholds compare candidate queued-provider evidence against the accepted
/// borrowed/baseline contour. They are local readiness gates, not production SLA
/// guarantees.
/// </remarks>
public sealed record RadarProcessingQueuedProviderRolloutThresholds
{
    /// <summary>
    /// Required release failure count for rollout acceptance.
    /// </summary>
    public const long RequiredReleaseFailureCount = 0;

    /// <summary>
    /// Required retained batch count at the end of a candidate run.
    /// </summary>
    public const long RequiredCurrentRetainedBatchCount = 0;

    /// <summary>
    /// Required retained payload bytes at the end of a candidate run.
    /// </summary>
    public const long RequiredCurrentRetainedPayloadBytes = 0;

    /// <summary>
    /// Default combined retained payload budget for queued-provider evidence.
    /// </summary>
    public const long DefaultCombinedRetainedPayloadBytesBudget = 536_870_912;

    /// <summary>
    /// Default maximum allocation ratio versus the borrowed reference.
    /// </summary>
    public const double DefaultMaximumCandidateToBorrowedAllocationRatio = 1.10;

    /// <summary>
    /// Default maximum elapsed-time ratio versus the borrowed reference.
    /// </summary>
    public const double DefaultMaximumCandidateToBorrowedElapsedRatio = 1.00;

    /// <summary>
    /// Default maximum accepted candidate run spread.
    /// </summary>
    public const double DefaultMaximumCandidateRunSpreadRatio = 0.075;

    /// <summary>
    /// Creates rollout thresholds with positive ratios and budget.
    /// </summary>
    public RadarProcessingQueuedProviderRolloutThresholds(
        long combinedRetainedPayloadBytesBudget = DefaultCombinedRetainedPayloadBytesBudget,
        double maximumCandidateToBorrowedAllocationRatio = DefaultMaximumCandidateToBorrowedAllocationRatio,
        double maximumCandidateToBorrowedElapsedRatio = DefaultMaximumCandidateToBorrowedElapsedRatio,
        double maximumCandidateRunSpreadRatio = DefaultMaximumCandidateRunSpreadRatio)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(combinedRetainedPayloadBytesBudget);
        ThrowIfNotPositive(
            maximumCandidateToBorrowedAllocationRatio,
            nameof(maximumCandidateToBorrowedAllocationRatio));
        ThrowIfNotPositive(
            maximumCandidateToBorrowedElapsedRatio,
            nameof(maximumCandidateToBorrowedElapsedRatio));
        ArgumentOutOfRangeException.ThrowIfNegative(maximumCandidateRunSpreadRatio);

        CombinedRetainedPayloadBytesBudget = combinedRetainedPayloadBytesBudget;
        MaximumCandidateToBorrowedAllocationRatio = maximumCandidateToBorrowedAllocationRatio;
        MaximumCandidateToBorrowedElapsedRatio = maximumCandidateToBorrowedElapsedRatio;
        MaximumCandidateRunSpreadRatio = maximumCandidateRunSpreadRatio;
    }

    /// <summary>
    /// Maximum combined retained payload bytes allowed during evidence capture.
    /// </summary>
    public long CombinedRetainedPayloadBytesBudget { get; }

    /// <summary>
    /// Maximum candidate allocation ratio compared with the borrowed reference.
    /// </summary>
    public double MaximumCandidateToBorrowedAllocationRatio { get; }

    /// <summary>
    /// Maximum candidate elapsed-time ratio compared with the borrowed reference.
    /// </summary>
    public double MaximumCandidateToBorrowedElapsedRatio { get; }

    /// <summary>
    /// Maximum accepted spread between candidate runs.
    /// </summary>
    public double MaximumCandidateRunSpreadRatio { get; }

    /// <summary>
    /// Accepted default rollout thresholds.
    /// </summary>
    public static RadarProcessingQueuedProviderRolloutThresholds Default { get; } = new();

    private static void ThrowIfNotPositive(
        double value,
        string paramName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }
    }
}
