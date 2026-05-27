namespace RadarPulse.Domain.Processing;

/// <summary>
/// Rejection reasons for publishing a partition owner move.
/// </summary>
public enum RadarProcessingTopologyMoveError
{
    /// <summary>
    /// No error; the move was accepted.
    /// </summary>
    None = 0,

    /// <summary>
    /// The request targeted an older topology version.
    /// </summary>
    StaleTopologyVersion,

    /// <summary>
    /// The requested partition id is outside the topology shape.
    /// </summary>
    PartitionIdOutOfRange,

    /// <summary>
    /// The source shard id is outside the topology shape.
    /// </summary>
    SourceShardIdOutOfRange,

    /// <summary>
    /// The target shard id is outside the topology shape.
    /// </summary>
    TargetShardIdOutOfRange,

    /// <summary>
    /// Source and target shards are the same.
    /// </summary>
    NoOpMove,

    /// <summary>
    /// The source shard no longer owns the requested partition.
    /// </summary>
    SourceShardOwnershipMismatch
}
