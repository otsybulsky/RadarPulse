namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingTelemetryRetentionOptions
{
    public static RadarProcessingTelemetryRetentionOptions Default { get; } = new();

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

    public RadarProcessingDiagnosticRetentionMode RetentionMode { get; }

    public int MaxRetainedDecisions { get; }

    public int MaxRetainedLifecycleTransitions { get; }

    public int MaxRetainedAcceptedMoves { get; }

    public int MaxRetainedValidationFailures { get; }

    public int MaxRetainedWorkerBatches { get; }

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
