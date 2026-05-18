namespace RadarPulse.Infrastructure.Processing;

public enum RadarProcessingSyntheticRebalanceWorkloadKind
{
    Balanced = 0,
    SustainedHotShard,
    IntrinsicHotPartition,
    OscillatingSpike,
    CooldownStorm,
    QuarantineTtlRetry,
    QuarantineSustainedCoolingClear,
    QuarantinePressureChangeRetry,
    QuarantineRetryReentry,
    QuarantineSuccessfulReliefClear
}
