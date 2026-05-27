namespace RadarPulse.Domain.Processing;

public enum RadarProcessingTopologyMoveError
{
    None = 0,
    StaleTopologyVersion,
    PartitionIdOutOfRange,
    SourceShardIdOutOfRange,
    TargetShardIdOutOfRange,
    NoOpMove,
    SourceShardOwnershipMismatch
}
