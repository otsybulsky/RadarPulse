namespace RadarPulse.Domain.Processing;

public static partial class RadarProcessingQueuedProviderReadinessEvaluator
{
    public static RadarProcessingQueuedProviderReadinessResult EvaluateNaturalEvidence(
        bool isDefaultCandidateContour,
        TimeSpan overlapConsumerDelay)
    {
        if (overlapConsumerDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(overlapConsumerDelay));
        }

        if (overlapConsumerDelay > TimeSpan.Zero)
        {
            return RadarProcessingQueuedProviderReadinessResult.Failed(
                RadarProcessingQueuedProviderReadinessGate.NaturalEvidence,
                RadarProcessingQueuedProviderReadinessError.ControlledProofExcluded,
                "Controlled consumer-delay proof runs cannot satisfy natural default-readiness evidence.");
        }

        if (!isDefaultCandidateContour)
        {
            return RadarProcessingQueuedProviderReadinessResult.Inconclusive(
                RadarProcessingQueuedProviderReadinessGate.EffectiveConfiguration,
                RadarProcessingQueuedProviderReadinessError.CandidateContourMismatch,
                "Natural readiness requires the exact queued-owned default-candidate contour.");
        }

        return RadarProcessingQueuedProviderReadinessResult.Passed(
            RadarProcessingQueuedProviderReadinessGate.NaturalEvidence);
    }

    /// <summary>
    /// Evaluates candidate elapsed time against a borrowed reference duration.
    /// </summary>
    public static RadarProcessingQueuedProviderReadinessResult EvaluatePerformanceDelta(
        TimeSpan candidateElapsed,
        TimeSpan borrowedReferenceElapsed,
        double maximumCandidateToReferenceRatio)
    {
        EnsureNonNegative(candidateElapsed, nameof(candidateElapsed));
        EnsureNonNegative(borrowedReferenceElapsed, nameof(borrowedReferenceElapsed));
        if (maximumCandidateToReferenceRatio <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCandidateToReferenceRatio));
        }

        if (borrowedReferenceElapsed == TimeSpan.Zero)
        {
            return RadarProcessingQueuedProviderReadinessResult.Inconclusive(
                RadarProcessingQueuedProviderReadinessGate.PerformanceDelta,
                RadarProcessingQueuedProviderReadinessError.MissingBorrowedReference,
                "Performance readiness requires a positive same-run blocking-borrowed reference duration.");
        }

        var ratio = candidateElapsed.TotalSeconds / borrowedReferenceElapsed.TotalSeconds;
        if (ratio > maximumCandidateToReferenceRatio)
        {
            return RadarProcessingQueuedProviderReadinessResult.Failed(
                RadarProcessingQueuedProviderReadinessGate.PerformanceDelta,
                RadarProcessingQueuedProviderReadinessError.PerformanceRegression,
                "Queued-owned candidate elapsed time exceeded the configured reference ratio.",
                expectedRatio: maximumCandidateToReferenceRatio,
                actualRatio: ratio);
        }

        return RadarProcessingQueuedProviderReadinessResult.Passed(
            RadarProcessingQueuedProviderReadinessGate.PerformanceDelta);
    }
}
