namespace RadarPulse.Domain.Processing;

public enum RadarProcessingPartitionMigrationState
{
    None = 0,
    RejectedDecision,
    ValidationFailed,
    Published
}
