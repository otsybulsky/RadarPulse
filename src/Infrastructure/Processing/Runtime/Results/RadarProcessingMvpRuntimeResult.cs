namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingMvpRuntimeResult
{
    public RadarProcessingMvpRuntimeResult(
        RadarProcessingMvpRuntimePlan plan,
        RadarProcessingArchiveQueuedOverlapResult overlapResult)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(overlapResult);

        Plan = plan;
        OverlapResult = overlapResult;
    }

    public RadarProcessingMvpRuntimePlan Plan { get; }

    public RadarProcessingArchiveQueuedOverlapResult OverlapResult { get; }
}

