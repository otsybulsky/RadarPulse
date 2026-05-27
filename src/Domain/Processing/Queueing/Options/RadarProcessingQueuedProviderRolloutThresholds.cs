namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingQueuedProviderRolloutThresholds
{
    public const long RequiredReleaseFailureCount = 0;
    public const long RequiredCurrentRetainedBatchCount = 0;
    public const long RequiredCurrentRetainedPayloadBytes = 0;
    public const long DefaultCombinedRetainedPayloadBytesBudget = 536_870_912;
    public const double DefaultMaximumCandidateToBorrowedAllocationRatio = 1.10;
    public const double DefaultMaximumCandidateToBorrowedElapsedRatio = 1.00;
    public const double DefaultMaximumCandidateRunSpreadRatio = 0.075;

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

    public long CombinedRetainedPayloadBytesBudget { get; }

    public double MaximumCandidateToBorrowedAllocationRatio { get; }

    public double MaximumCandidateToBorrowedElapsedRatio { get; }

    public double MaximumCandidateRunSpreadRatio { get; }

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
