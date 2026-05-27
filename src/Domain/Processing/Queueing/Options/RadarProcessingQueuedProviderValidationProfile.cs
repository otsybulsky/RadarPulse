namespace RadarPulse.Domain.Processing;

/// <summary>
/// Validation depth for queued-provider semantic checks.
/// </summary>
public enum RadarProcessingQueuedProviderValidationProfile
{
    /// <summary>
    /// Validation is disabled.
    /// </summary>
    Off = 0,

    /// <summary>
    /// Run the minimum checks required for safe operation.
    /// </summary>
    Essential = 1,

    /// <summary>
    /// Include additional diagnostic checks and counters.
    /// </summary>
    Diagnostic = 2,

    /// <summary>
    /// Include benchmark/readiness-grade validation evidence.
    /// </summary>
    Benchmark = 3
}
