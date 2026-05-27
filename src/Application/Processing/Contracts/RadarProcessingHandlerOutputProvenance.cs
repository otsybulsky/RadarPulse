namespace RadarPulse.Application.Processing;

/// <summary>
/// Provenance for handler output exposed in BFF/product diagnostics.
/// </summary>
public enum RadarProcessingHandlerOutputProvenance
{
    /// <summary>
    /// Output came from handler-free ordered concurrent processing.
    /// </summary>
    HandlerFreeOrderedConcurrent = 1,

    /// <summary>
    /// Output came from committed snapshots through sequential fallback.
    /// </summary>
    StatefulSequentialFallback = 2,

    /// <summary>
    /// Output came from ordered handler delta/merge.
    /// </summary>
    OrderedHandlerDeltaMerge = 3,

    /// <summary>
    /// Handler output is blocked by an unsupported handler set.
    /// </summary>
    UnsupportedHandlerSet = 4
}
