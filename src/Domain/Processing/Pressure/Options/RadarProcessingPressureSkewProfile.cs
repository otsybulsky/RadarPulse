namespace RadarPulse.Domain.Processing;

public enum RadarProcessingPressureSkewProfile
{
    None = 0,
    HotShard = 1,
    RotatingHotShard = 2,
    HotPartition = 3,
    TargetStarvation = 4,
    BudgetStorm = 5
}
