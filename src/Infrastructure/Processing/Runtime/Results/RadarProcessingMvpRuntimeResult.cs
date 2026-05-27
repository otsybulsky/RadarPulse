namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// MVP runtime result including the selected plan and queued-overlap outcome.
/// </summary>
public sealed class RadarProcessingMvpRuntimeResult
{
    /// <summary>
    /// Creates an MVP runtime result.
    /// </summary>
    public RadarProcessingMvpRuntimeResult(
        RadarProcessingMvpRuntimePlan plan,
        RadarProcessingArchiveQueuedOverlapResult overlapResult)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(overlapResult);

        Plan = plan;
        OverlapResult = overlapResult;
    }

    /// <summary>
    /// Runtime plan used for the run.
    /// </summary>
    public RadarProcessingMvpRuntimePlan Plan { get; }

    /// <summary>
    /// Queued producer/consumer overlap result.
    /// </summary>
    public RadarProcessingArchiveQueuedOverlapResult OverlapResult { get; }
}
