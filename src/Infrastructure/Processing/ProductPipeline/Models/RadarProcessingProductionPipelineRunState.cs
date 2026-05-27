namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Operator-facing lifecycle state for a production-pipeline run.
/// </summary>
public enum RadarProcessingProductionPipelineRunState
{
    /// <summary>
    /// Run has not started.
    /// </summary>
    NotStarted = 1,

    /// <summary>
    /// Run is actively accepting or processing work.
    /// </summary>
    Running = 2,

    /// <summary>
    /// Run is draining accepted durable work.
    /// </summary>
    Draining = 3,

    /// <summary>
    /// Run completed and passed readiness checks.
    /// </summary>
    Completed = 4,

    /// <summary>
    /// Run is stopped for new work with durable state preserved.
    /// </summary>
    Stopped = 5,

    /// <summary>
    /// Run is blocked by configuration, handler, durable, or retained-resource evidence.
    /// </summary>
    Blocked = 6,

    /// <summary>
    /// Run failed during processing or recovery.
    /// </summary>
    Failed = 7,

    /// <summary>
    /// Run was canceled.
    /// </summary>
    Canceled = 8
}
