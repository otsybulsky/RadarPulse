namespace RadarPulse.Domain.Processing;

/// <summary>
/// Semantic surface covered by queued-provider validation.
/// </summary>
public enum RadarProcessingQueuedProviderValidationSurface
{
    /// <summary>
    /// Validate processing output and queue ordering only.
    /// </summary>
    ProcessingOnly = 1,

    /// <summary>
    /// Validate processing plus topology and rebalance evidence.
    /// </summary>
    Rebalance = 2
}
