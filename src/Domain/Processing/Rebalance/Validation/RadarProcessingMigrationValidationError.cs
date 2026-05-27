namespace RadarPulse.Domain.Processing;

public enum RadarProcessingMigrationValidationError
{
    None = 0,
    DecisionNotAcceptedMove,
    MissingCandidate,
    StaleTopologyVersion,
    PartitionIdOutOfRange,
    SourceShardIdOutOfRange,
    TargetShardIdOutOfRange,
    NoOpMove,
    SourceShardOwnershipMismatch
}
