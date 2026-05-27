namespace RadarPulse.Domain.Processing;

/// <summary>
/// Source and target pressure before and after a projected rebalance move.
/// </summary>
public readonly record struct RadarProcessingProjectedPressure
{
    /// <summary>
    /// Creates a projected pressure snapshot.
    /// </summary>
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

    /// <summary>
    /// Source shard pressure before the projected move.
    /// </summary>
    public RadarProcessingPressureScore SourceShardBefore { get; }

    /// <summary>
    /// Target shard pressure before the projected move.
    /// </summary>
    public RadarProcessingPressureScore TargetShardBefore { get; }

    /// <summary>
    /// Source shard pressure after the projected move.
    /// </summary>
    public RadarProcessingPressureScore SourceShardAfter { get; }

    /// <summary>
    /// Target shard pressure after the projected move.
    /// </summary>
    public RadarProcessingPressureScore TargetShardAfter { get; }

    /// <summary>
    /// Empty projected pressure value.
    /// </summary>
    public static RadarProcessingProjectedPressure Zero => default;
}
