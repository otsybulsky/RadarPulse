namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Resolved option value plus the source that supplied it.
/// </summary>
public sealed record RadarProcessingProductionPipelineResolvedOption<T>(
    /// <summary>
    /// Effective option value.
    /// </summary>
    T Value,

    /// <summary>
    /// Source of the effective value.
    /// </summary>
    RadarProcessingProductionPipelineOptionSource Source);
