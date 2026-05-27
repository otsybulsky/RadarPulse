namespace RadarPulse.Domain.Processing;

/// <summary>
/// Validation errors that can reject a rebalance migration.
/// </summary>
public enum RadarProcessingMigrationValidationError
{
    /// <summary>
    /// No validation error.
    /// </summary>
    None = 0,

    /// <summary>
    /// The source decision was not an accepted move.
    /// </summary>
    DecisionNotAcceptedMove,

    /// <summary>
    /// The source decision did not carry a candidate.
    /// </summary>
    MissingCandidate,

    /// <summary>
    /// The migration expected an older topology version.
    /// </summary>
    StaleTopologyVersion,

    /// <summary>
    /// The migration partition id is outside the topology shape.
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
    /// Source and target shards are identical.
    /// </summary>
    NoOpMove,

    /// <summary>
    /// The source shard no longer owns the partition.
    /// </summary>
    SourceShardOwnershipMismatch
}
