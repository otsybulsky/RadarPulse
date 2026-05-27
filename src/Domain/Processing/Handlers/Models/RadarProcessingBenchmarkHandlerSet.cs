namespace RadarPulse.Domain.Processing;

/// <summary>
/// Built-in handler sets used by processing benchmark and readiness workloads.
/// </summary>
public enum RadarProcessingBenchmarkHandlerSet
{
    /// <summary>
    /// No custom handlers.
    /// </summary>
    None = 0,

    /// <summary>
    /// Lightweight counter/checksum mergeable handlers.
    /// </summary>
    CounterChecksum,

    /// <summary>
    /// Heavier counter/checksum mergeable handlers used by high-volume gates.
    /// </summary>
    CounterChecksumHeavy
}
