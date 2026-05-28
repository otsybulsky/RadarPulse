namespace RadarPulse.Domain.Processing;

public static partial class RadarProcessingQueuedProviderReadinessEvaluator
{
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
}
