namespace RadarPulse.Domain.Processing;

/// <summary>
/// Selects how aggressively processing validation checks accepted runtime contracts.
/// </summary>
public enum RadarProcessingValidationProfile
{
    /// <summary>
    /// Skips validation checks.
    /// </summary>
    Off = 0,

    /// <summary>
    /// Runs only checks required to detect contract-breaking failures.
    /// </summary>
    Essential = 1,

    /// <summary>
    /// Runs diagnostic consistency checks suitable for operator evidence.
    /// </summary>
    Diagnostic = 2,

    /// <summary>
    /// Runs deterministic comparison checks used by benchmark and parity flows.
    /// </summary>
    Benchmark = 3
}
