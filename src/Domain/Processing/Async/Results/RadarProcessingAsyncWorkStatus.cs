namespace RadarPulse.Domain.Processing;

/// <summary>
/// Terminal status for an async work item.
/// </summary>
public enum RadarProcessingAsyncWorkStatus : byte
{
    /// <summary>
    /// Work completed and produced accepted metrics.
    /// </summary>
    Succeeded = 1,

    /// <summary>
    /// Work failed before producing accepted metrics.
    /// </summary>
    Failed = 2,

    /// <summary>
    /// Work was canceled before completion.
    /// </summary>
    Canceled = 3
}
