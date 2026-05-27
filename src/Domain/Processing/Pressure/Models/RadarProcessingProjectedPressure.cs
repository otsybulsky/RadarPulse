namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingProjectedPressure
{
    public RadarProcessingProjectedPressure(
        RadarProcessingPressureScore sourceShardBefore,
        RadarProcessingPressureScore targetShardBefore,
        RadarProcessingPressureScore sourceShardAfter,
        RadarProcessingPressureScore targetShardAfter)
    {
        SourceShardBefore = sourceShardBefore;
        TargetShardBefore = targetShardBefore;
        SourceShardAfter = sourceShardAfter;
        TargetShardAfter = targetShardAfter;
    }

    public RadarProcessingPressureScore SourceShardBefore { get; }

    public RadarProcessingPressureScore TargetShardBefore { get; }

    public RadarProcessingPressureScore SourceShardAfter { get; }

    public RadarProcessingPressureScore TargetShardAfter { get; }

    public static RadarProcessingProjectedPressure Zero => default;
}
