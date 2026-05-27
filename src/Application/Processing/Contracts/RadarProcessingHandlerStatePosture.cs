namespace RadarPulse.Application.Processing;

/// <summary>
/// Product-facing posture for the configured handler set.
/// </summary>
public enum RadarProcessingHandlerStatePosture
{
    /// <summary>
    /// No handlers are configured, so ordered concurrent processing is safe.
    /// </summary>
    HandlerFreeOrderedConcurrent = 1,

    /// <summary>
    /// Stateful handlers require committed snapshots and sequential fallback.
    /// </summary>
    StatefulSnapshotSequentialFallback = 2,

    /// <summary>
    /// All handlers are eligible for ordered handler delta/merge output.
    /// </summary>
    MergeableHandlerDeltaMergeEligible = 3,

    /// <summary>
    /// At least one handler is unsupported by the MVP runtime surface.
    /// </summary>
    UnsupportedHandlerSet = 4
}
