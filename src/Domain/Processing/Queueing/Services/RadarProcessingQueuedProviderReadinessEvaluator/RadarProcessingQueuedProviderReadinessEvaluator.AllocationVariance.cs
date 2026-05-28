namespace RadarPulse.Domain.Processing;

public static partial class RadarProcessingQueuedProviderReadinessEvaluator
{
    public static RadarProcessingQueuedProviderReadinessResult EvaluateAllocationMovement(
        long candidateAllocatedBytes,
        long? referenceAllocatedBytes,
        double maximumCandidateToReferenceRatio)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(candidateAllocatedBytes);
        if (referenceAllocatedBytes is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(referenceAllocatedBytes));
        }

        if (maximumCandidateToReferenceRatio <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCandidateToReferenceRatio));
        }

        if (referenceAllocatedBytes is not { } referenceBytes)
        {
            return RadarProcessingQueuedProviderReadinessResult.NotEvaluated(
                RadarProcessingQueuedProviderReadinessGate.AllocationMovement,
                "Allocation readiness requires a snapshot-copy or borrowed reference allocation measurement.");
        }

        if (referenceBytes == 0)
        {
            return RadarProcessingQueuedProviderReadinessResult.Inconclusive(
                RadarProcessingQueuedProviderReadinessGate.AllocationMovement,
                RadarProcessingQueuedProviderReadinessError.AllocationRegression,
                "Allocation readiness requires a positive reference allocation measurement.",
                expectedBytes: referenceBytes,
                actualBytes: candidateAllocatedBytes);
        }

        var ratio = (double)candidateAllocatedBytes / referenceBytes;
        if (ratio > maximumCandidateToReferenceRatio)
        {
            return RadarProcessingQueuedProviderReadinessResult.Failed(
                RadarProcessingQueuedProviderReadinessGate.AllocationMovement,
                RadarProcessingQueuedProviderReadinessError.AllocationRegression,
                "Queued-owned candidate allocation exceeded the configured reference ratio.",
                expectedBytes: referenceBytes,
                actualBytes: candidateAllocatedBytes,
                expectedRatio: maximumCandidateToReferenceRatio,
                actualRatio: ratio);
        }

        return RadarProcessingQueuedProviderReadinessResult.Passed(
            RadarProcessingQueuedProviderReadinessGate.AllocationMovement);
    }

    /// <summary>
    /// Evaluates relative standard deviation across repeated candidate runs.
    /// </summary>
    public static RadarProcessingQueuedProviderReadinessResult EvaluateRunVariance(
        double? candidateRelativeStandardDeviation,
        double maximumRelativeStandardDeviation)
    {
        if (candidateRelativeStandardDeviation is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(candidateRelativeStandardDeviation));
        }

        if (maximumRelativeStandardDeviation < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRelativeStandardDeviation));
        }

        if (candidateRelativeStandardDeviation is not { } variance)
        {
            return RadarProcessingQueuedProviderReadinessResult.NotEvaluated(
                RadarProcessingQueuedProviderReadinessGate.RunVariance,
                "Run variance readiness requires repeated natural candidate measurements.");
        }

        if (variance > maximumRelativeStandardDeviation)
        {
            return RadarProcessingQueuedProviderReadinessResult.Failed(
                RadarProcessingQueuedProviderReadinessGate.RunVariance,
                RadarProcessingQueuedProviderReadinessError.RunVarianceTooHigh,
                "Repeated natural candidate measurements exceeded the configured variance threshold.",
                expectedRatio: maximumRelativeStandardDeviation,
                actualRatio: variance);
        }

        return RadarProcessingQueuedProviderReadinessResult.Passed(
            RadarProcessingQueuedProviderReadinessGate.RunVariance);
    }

    /// <summary>
    /// Evaluates elapsed-time spread across repeated candidate runs.
    /// </summary>
    public static RadarProcessingQueuedProviderReadinessResult EvaluateRunSpread(
        TimeSpan? candidateRunSpread,
        TimeSpan? candidateAverageElapsed,
        double maximumSpreadToAverageRatio)
    {
        if (candidateRunSpread is { } suppliedSpread &&
            suppliedSpread < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(candidateRunSpread));
        }

        if (candidateAverageElapsed is { } suppliedAverage &&
            suppliedAverage < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(candidateAverageElapsed));
        }

        if (maximumSpreadToAverageRatio < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumSpreadToAverageRatio));
        }

        if (candidateRunSpread is not { } spread ||
            candidateAverageElapsed is not { } average)
        {
            return RadarProcessingQueuedProviderReadinessResult.NotEvaluated(
                RadarProcessingQueuedProviderReadinessGate.RunVariance,
                "Run spread readiness requires repeated natural candidate measurements.");
        }

        if (average == TimeSpan.Zero)
        {
            return RadarProcessingQueuedProviderReadinessResult.Inconclusive(
                RadarProcessingQueuedProviderReadinessGate.RunVariance,
                RadarProcessingQueuedProviderReadinessError.RunVarianceTooHigh,
                "Run spread readiness requires a positive candidate average duration.");
        }

        var ratio = spread.TotalSeconds / average.TotalSeconds;
        if (ratio > maximumSpreadToAverageRatio)
        {
            return RadarProcessingQueuedProviderReadinessResult.Failed(
                RadarProcessingQueuedProviderReadinessGate.RunVariance,
                RadarProcessingQueuedProviderReadinessError.RunVarianceTooHigh,
                "Repeated natural candidate run spread exceeded the configured threshold.",
                expectedRatio: maximumSpreadToAverageRatio,
                actualRatio: ratio);
        }

        return RadarProcessingQueuedProviderReadinessResult.Passed(
            RadarProcessingQueuedProviderReadinessGate.RunVariance);
    }
}
