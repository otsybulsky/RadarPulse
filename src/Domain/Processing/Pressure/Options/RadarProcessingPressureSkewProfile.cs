namespace RadarPulse.Domain.Processing;

/// <summary>
/// Synthetic pressure skew profile used by local benchmark and rebalance exercises.
/// </summary>
public enum RadarProcessingPressureSkewProfile
{
    /// <summary>
    /// Do not alter pressure samples.
    /// </summary>
    None = 0,

    /// <summary>
    /// Concentrate pressure on one shard.
    /// </summary>
    HotShard = 1,

    /// <summary>
    /// Rotate the hot shard across evaluation periods.
    /// </summary>
    RotatingHotShard = 2,

    /// <summary>
    /// Concentrate pressure on one partition.
    /// </summary>
    HotPartition = 3,

    /// <summary>
    /// Raise all shards enough to starve cold targets.
    /// </summary>
    TargetStarvation = 4,

    /// <summary>
    /// Keep one shard hot to exercise rebalance budget behavior.
    /// </summary>
    BudgetStorm = 5
}
