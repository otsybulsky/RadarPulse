namespace RadarPulse.Domain.Processing;

/// <summary>
/// Describes whether a source handler is safe for ordered concurrent handler output.
/// </summary>
public enum RadarSourceProcessingHandlerExecutionClassification
{
    /// <summary>
    /// Handler output can be represented as mergeable per-batch deltas.
    /// </summary>
    Mergeable = 1,

    /// <summary>
    /// Handler output is available only from committed source snapshots.
    /// </summary>
    SnapshotOnly = 2,

    /// <summary>
    /// Handler cannot participate in the accepted runtime/product surface.
    /// </summary>
    Unsupported = 3
}
