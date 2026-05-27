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
public static class RadarProcessingQueuedProviderReadinessEvaluator
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
    /// </summary>
    public static RadarProcessingQueuedProviderReadinessResult EvaluateRetainedResourceReleaseHealth(
        RadarProcessingRetainedPayloadTelemetrySummary telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.FailedRetentionCount > 0)
        {
            return RadarProcessingQueuedProviderReadinessResult.Failed(
                RadarProcessingQueuedProviderReadinessGate.RetainedResourceReleaseHealth,
                RadarProcessingQueuedProviderReadinessError.RetainedResourceRetentionFailed,
                "Retained payload acquisition failures prevent queued-owned default readiness.",
                expectedCount: 0,
                actualCount: telemetry.FailedRetentionCount);
        }

        if (telemetry.ReleaseFailedCount > 0)
        {
            return RadarProcessingQueuedProviderReadinessResult.Failed(
                RadarProcessingQueuedProviderReadinessGate.RetainedResourceReleaseHealth,
                RadarProcessingQueuedProviderReadinessError.RetainedResourceReleaseFailed,
                "Retained resource release failures prevent queued-owned default readiness.",
                expectedCount: 0,
                actualCount: telemetry.ReleaseFailedCount);
        }

        var completedReleaseCount = checked(
            telemetry.ReleasedBatchCount +
            telemetry.AlreadyReleasedBatchCount +
            telemetry.ReleaseNotRequiredCount);
        if (completedReleaseCount < telemetry.RetainedBatchCount)
        {
            return RadarProcessingQueuedProviderReadinessResult.Failed(
                RadarProcessingQueuedProviderReadinessGate.RetainedResourceReleaseHealth,
                RadarProcessingQueuedProviderReadinessError.RetainedResourceCleanupIncomplete,
                "Every retained batch must be released or explicitly marked as release-not-required.",
                expectedCount: telemetry.RetainedBatchCount,
                actualCount: completedReleaseCount);
        }

        return RadarProcessingQueuedProviderReadinessResult.Passed(
            RadarProcessingQueuedProviderReadinessGate.RetainedResourceReleaseHealth);
    }

    /// <summary>
    /// Evaluates retained-resource pressure against the supplied combined payload budget.
    /// </summary>
    public static RadarProcessingQueuedProviderReadinessResult EvaluateRetainedResourcePressure(
        RadarProcessingRetainedResourcePressureSummary? pressure,
        long? combinedRetainedPayloadBytesBudget = null,
        bool requiresActiveRetainedTelemetry = false)
    {
        if (combinedRetainedPayloadBytesBudget is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(combinedRetainedPayloadBytesBudget));
        }

        if (pressure is null)
        {
            return RadarProcessingQueuedProviderReadinessResult.Inconclusive(
                RadarProcessingQueuedProviderReadinessGate.RetainedResourcePressure,
                RadarProcessingQueuedProviderReadinessError.MissingRetainedResourcePressureTelemetry,
                "Retained-resource pressure telemetry is required for queued-owned default readiness.");
        }

        if (requiresActiveRetainedTelemetry &&
            pressure.CombinedRetainedBatchCountHighWatermark > 0 &&
            pressure.ActiveRetainedBatchCountHighWatermark == 0)
        {
            return RadarProcessingQueuedProviderReadinessResult.Inconclusive(
                RadarProcessingQueuedProviderReadinessGate.RetainedResourcePressure,
                RadarProcessingQueuedProviderReadinessError.MissingActiveRetainedTelemetry,
                "Active retained-resource pressure telemetry is required when retained batches are observed.",
                expectedCount: 1,
                actualCount: pressure.ActiveRetainedBatchCountHighWatermark);
        }

        if (combinedRetainedPayloadBytesBudget is not { } budget)
        {
            return RadarProcessingQueuedProviderReadinessResult.NotEvaluated(
                RadarProcessingQueuedProviderReadinessGate.RetainedResourcePressure,
                "Retained-resource pressure budget was not supplied.");
        }

        if (pressure.CombinedRetainedPayloadBytesHighWatermark > budget)
        {
            return RadarProcessingQueuedProviderReadinessResult.Failed(
                RadarProcessingQueuedProviderReadinessGate.RetainedResourcePressure,
                RadarProcessingQueuedProviderReadinessError.CombinedRetainedPayloadBudgetExceeded,
                "Combined pending-plus-active retained payload bytes exceeded the configured readiness budget.",
                expectedBytes: budget,
                actualBytes: pressure.CombinedRetainedPayloadBytesHighWatermark);
        }

        return RadarProcessingQueuedProviderReadinessResult.Passed(
            RadarProcessingQueuedProviderReadinessGate.RetainedResourcePressure);
    }

    /// <summary>
    /// Evaluates whether retained-resource pressure returned to the required terminal state.
    /// </summary>
    public static RadarProcessingQueuedProviderReadinessResult EvaluateRetainedResourceCleanupCompletion(
        RadarProcessingRetainedResourcePressureSummary? pressure)
    {
        if (pressure is null)
        {
            return RadarProcessingQueuedProviderReadinessResult.Inconclusive(
                RadarProcessingQueuedProviderReadinessGate.RetainedResourcePressure,
                RadarProcessingQueuedProviderReadinessError.MissingRetainedResourcePressureTelemetry,
                "Retained-resource pressure telemetry is required to verify cleanup completion.");
        }

        if (pressure.CurrentCombinedRetainedBatchCount !=
                RadarProcessingQueuedProviderRolloutThresholds.RequiredCurrentRetainedBatchCount ||
            pressure.CurrentCombinedRetainedPayloadBytes !=
                RadarProcessingQueuedProviderRolloutThresholds.RequiredCurrentRetainedPayloadBytes)
        {
            return RadarProcessingQueuedProviderReadinessResult.Failed(
                RadarProcessingQueuedProviderReadinessGate.RetainedResourcePressure,
                RadarProcessingQueuedProviderReadinessError.RetainedResourceCleanupIncomplete,
                "Retained-resource cleanup must return pending, active, and combined pressure to zero.",
                expectedCount: RadarProcessingQueuedProviderRolloutThresholds.RequiredCurrentRetainedBatchCount,
                actualCount: pressure.CurrentCombinedRetainedBatchCount,
                expectedBytes: RadarProcessingQueuedProviderRolloutThresholds.RequiredCurrentRetainedPayloadBytes,
                actualBytes: pressure.CurrentCombinedRetainedPayloadBytes);
        }

        return RadarProcessingQueuedProviderReadinessResult.Passed(
            RadarProcessingQueuedProviderReadinessGate.RetainedResourcePressure);
    }

    /// <summary>
    /// Evaluates whether evidence came from the natural default-candidate contour.
    /// </summary>
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

    /// <summary>
    /// Evaluates candidate allocation movement against a reference allocation measurement.
    /// </summary>
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

    private static RadarProcessingQueuedProviderReadinessGate SelectCorrectnessGate(
        RadarProcessingQueuedProviderValidationError error) =>
        error is RadarProcessingQueuedProviderValidationError.AcceptedMoveCountMismatch or
            RadarProcessingQueuedProviderValidationError.SkippedDecisionCountMismatch or
            RadarProcessingQueuedProviderValidationError.FailedMigrationCountMismatch or
            RadarProcessingQueuedProviderValidationError.FinalTopologyVersionMismatch or
            RadarProcessingQueuedProviderValidationError.ReferenceSemanticSurfaceMismatch
            ? RadarProcessingQueuedProviderReadinessGate.TopologyAndRebalanceParity
            : RadarProcessingQueuedProviderReadinessGate.CorrectnessParity;

    private static RadarProcessingQueuedProviderReadinessError SelectCorrectnessError(
        RadarProcessingQueuedProviderValidationError error) =>
        error switch
        {
            RadarProcessingQueuedProviderValidationError.DeterministicChecksumMismatch =>
                RadarProcessingQueuedProviderReadinessError.ChecksumMismatch,
            RadarProcessingQueuedProviderValidationError.AcceptedMoveCountMismatch or
            RadarProcessingQueuedProviderValidationError.SkippedDecisionCountMismatch or
            RadarProcessingQueuedProviderValidationError.FailedMigrationCountMismatch or
            RadarProcessingQueuedProviderValidationError.FinalTopologyVersionMismatch or
            RadarProcessingQueuedProviderValidationError.ReferenceSemanticSurfaceMismatch =>
                RadarProcessingQueuedProviderReadinessError.TopologyOrRebalanceMismatch,
            _ => RadarProcessingQueuedProviderReadinessError.QueuedProviderValidationFailed
        };

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
