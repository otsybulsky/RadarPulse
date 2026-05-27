namespace RadarPulse.Domain.Processing;

/// <summary>
/// Controls how much diagnostic detail is retained beside processing counters.
/// </summary>
public enum RadarProcessingDiagnosticRetentionMode
{
    /// <summary>
    /// Retains aggregate counters only and drops recent diagnostic samples.
    /// </summary>
    Counters = 0,

    /// <summary>
    /// Retains bounded recent samples for normal operator inspection.
    /// </summary>
    Recent = 1,

    /// <summary>
    /// Retains the configured diagnostic detail limits for deeper troubleshooting.
    /// </summary>
    Diagnostic = 2
}
