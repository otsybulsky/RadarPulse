namespace RadarPulse.Domain.Processing;

/// <summary>
/// Configures bounded diagnostic retention for rebalance, validation, and async worker telemetry.
/// </summary>
public sealed record RadarProcessingTelemetryRetentionOptions
{
    /// <summary>
    /// Gets the default recent-detail retention policy.
    /// </summary>
    public static RadarProcessingTelemetryRetentionOptions Default { get; } = new();

    /// <summary>
    /// Creates retention limits for diagnostic samples and validates every limit as non-negative.
    /// </summary>
    public RadarProcessingTelemetryRetentionOptions(
        RadarProcessingDiagnosticRetentionMode retentionMode = RadarProcessingDiagnosticRetentionMode.Recent,
        int maxRetainedDecisions = 128,
        int maxRetainedLifecycleTransitions = 64,
        int maxRetainedAcceptedMoves = 64,
        int maxRetainedValidationFailures = 32,
        int maxRetainedWorkerBatches = 128,
        int maxRetainedWorkerFailures = 64)
    {
        EnsureKnownRetentionMode(retentionMode);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetainedDecisions);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetainedLifecycleTransitions);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetainedAcceptedMoves);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetainedValidationFailures);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetainedWorkerBatches);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetainedWorkerFailures);

        RetentionMode = retentionMode;
        MaxRetainedDecisions = maxRetainedDecisions;
        MaxRetainedLifecycleTransitions = maxRetainedLifecycleTransitions;
        MaxRetainedAcceptedMoves = maxRetainedAcceptedMoves;
        MaxRetainedValidationFailures = maxRetainedValidationFailures;
        MaxRetainedWorkerBatches = maxRetainedWorkerBatches;
        MaxRetainedWorkerFailures = maxRetainedWorkerFailures;
    }

    /// <summary>
    /// Gets the retention mode that decides whether only counters or recent diagnostic detail is kept.
    /// </summary>
    public RadarProcessingDiagnosticRetentionMode RetentionMode { get; }

    /// <summary>
    /// Gets the maximum retained rebalance planner decisions.
    /// </summary>
    public int MaxRetainedDecisions { get; }

    /// <summary>
    /// Gets the maximum retained topology lifecycle transitions.
    /// </summary>
    public int MaxRetainedLifecycleTransitions { get; }

    /// <summary>
    /// Gets the maximum retained accepted migration moves.
    /// </summary>
    public int MaxRetainedAcceptedMoves { get; }

    /// <summary>
    /// Gets the maximum retained migration validation failures.
    /// </summary>
    public int MaxRetainedValidationFailures { get; }

    /// <summary>
    /// Gets the maximum retained recent worker batch samples.
    /// </summary>
    public int MaxRetainedWorkerBatches { get; }

    /// <summary>
    /// Gets the maximum retained recent worker failure samples.
    /// </summary>
    public int MaxRetainedWorkerFailures { get; }

    internal static void EnsureKnownRetentionMode(
        RadarProcessingDiagnosticRetentionMode retentionMode)
    {
        if (retentionMode is not RadarProcessingDiagnosticRetentionMode.Counters and
            not RadarProcessingDiagnosticRetentionMode.Recent and
            not RadarProcessingDiagnosticRetentionMode.Diagnostic)
        {
            throw new ArgumentOutOfRangeException(nameof(retentionMode));
        }
    }
}
