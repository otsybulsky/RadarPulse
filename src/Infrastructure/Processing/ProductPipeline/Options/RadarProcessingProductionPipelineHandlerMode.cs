namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Handler output posture selected for the production pipeline.
/// </summary>
public enum RadarProcessingProductionPipelineHandlerMode
{
    /// <summary>
    /// Let the runner choose the accepted handler mode from the handler contract.
    /// </summary>
    Auto = 1,

    /// <summary>
    /// Run without custom handlers.
    /// </summary>
    HandlerFree = 2,

    /// <summary>
    /// Use mergeable handler deltas with ordered concurrent commit.
    /// </summary>
    MergeableDelta = 3,

    /// <summary>
    /// Use sequential snapshot publication for handlers that cannot merge deltas.
    /// </summary>
    SnapshotSequential = 4
}
