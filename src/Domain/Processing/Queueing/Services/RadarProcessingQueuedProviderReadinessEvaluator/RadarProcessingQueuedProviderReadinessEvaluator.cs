namespace RadarPulse.Domain.Processing;

/// <summary>
/// Evaluates queued-provider rollout readiness gates from validation and telemetry evidence.
/// </summary>
/// <remarks>
/// The evaluator converts raw validation, retained-resource, allocation,
/// performance, and variance evidence into named readiness gate results. These
/// gates support default-promotion decisions for the in-process queued-owned
/// provider contour.
/// </remarks>
public static partial class RadarProcessingQueuedProviderReadinessEvaluator
{
    /// <summary>
    /// Evaluates correctness parity between queued-provider output and the reference contour.
    /// </summary>
    public static RadarProcessingQueuedProviderReadinessResult EvaluateCorrectnessParity(
        RadarProcessingQueuedProviderValidationResult validation,
        bool hasBorrowedReference,
        bool requiresBorrowedReference = true)
    {
        ArgumentNullException.ThrowIfNull(validation);

        if (requiresBorrowedReference && !hasBorrowedReference)
        {
            return RadarProcessingQueuedProviderReadinessResult.Inconclusive(
                RadarProcessingQueuedProviderReadinessGate.CorrectnessParity,
                RadarProcessingQueuedProviderReadinessError.MissingBorrowedReference,
                "Default-readiness correctness requires a same-run blocking-borrowed reference.");
        }

        if (validation.IsValid)
        {
            return RadarProcessingQueuedProviderReadinessResult.Passed(
                RadarProcessingQueuedProviderReadinessGate.CorrectnessParity);
        }

        return RadarProcessingQueuedProviderReadinessResult.Failed(
            SelectCorrectnessGate(validation.Error),
            SelectCorrectnessError(validation.Error),
            validation.Message,
            validation.ExpectedChecksum,
            validation.ActualChecksum,
            validation.ExpectedCount,
            validation.ActualCount);
    }

    /// <summary>
    /// Evaluates whether retained payload acquisition and release completed cleanly.
}
