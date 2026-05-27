namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Control action applied to recoverable production-pipeline durable state.
/// </summary>
public enum RadarProcessingProductionPipelineFallbackAction
{
    /// <summary>
    /// No fallback action was applied.
    /// </summary>
    None = 1,

    /// <summary>
    /// Stop accepting new work while preserving durable state.
    /// </summary>
    StopAccepting = 2,

    /// <summary>
    /// Drain already accepted durable work.
    /// </summary>
    DrainAccepted = 3,

    /// <summary>
    /// Cancel open envelopes and release canceled retained resources.
    /// </summary>
    CancelOpenAndRelease = 4,

    /// <summary>
    /// Reject a fallback request that would leave accepted profile boundaries.
    /// </summary>
    RejectUnsafeFallback = 5
}
