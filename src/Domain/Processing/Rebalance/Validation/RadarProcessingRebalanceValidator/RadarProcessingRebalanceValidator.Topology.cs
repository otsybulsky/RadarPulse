namespace RadarPulse.Domain.Processing;

public static partial class RadarProcessingRebalanceValidator
{
    /// <summary>
    /// Validates that a topology publication advanced version while preserving shape.
    /// </summary>
    public static RadarProcessingRebalanceValidationResult ValidateTopologySequence(
        RadarProcessingTopology previous,
        RadarProcessingTopology current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        if (current.Version.Value <= previous.Version.Value)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.NonMonotonicTopologyVersion,
                "Current topology version must be greater than the previous topology version.");
        }

        if (previous.SourceUniverseVersion != current.SourceUniverseVersion ||
            previous.SourceCount != current.SourceCount ||
            previous.PartitionCount != current.PartitionCount ||
            previous.ShardCount != current.ShardCount)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.TopologyShapeMismatch,
                "Topology shape must remain stable across partition owner moves.");
        }

        for (var partitionId = 0; partitionId < previous.PartitionCount; partitionId++)
        {
            var previousPartition = previous.GetPartition(partitionId);
            var currentPartition = current.GetPartition(partitionId);

            if (previousPartition.PartitionId != partitionId ||
                currentPartition.PartitionId != partitionId ||
                (uint)previousPartition.ShardId >= (uint)previous.ShardCount ||
                (uint)currentPartition.ShardId >= (uint)current.ShardCount)
            {
                return Invalid(
                    RadarProcessingRebalanceValidationError.PartitionOwnerMismatch,
                    "Every partition must have exactly one in-range owner shard.");
            }

            if (previousPartition.SourceIdStart != currentPartition.SourceIdStart ||
                previousPartition.SourceIdEndExclusive != currentPartition.SourceIdEndExclusive)
            {
                return Invalid(
                    RadarProcessingRebalanceValidationError.SourcePartitionMappingChanged,
                    "SourceId to PartitionId mapping must remain stable across rebalance.");
            }
        }

        return RadarProcessingRebalanceValidationResult.Valid();
    }

    /// <summary>
    /// Validates that an accepted move changed only the migrated partition owner.
    /// </summary>
    public static RadarProcessingRebalanceValidationResult ValidateAcceptedMove(
        RadarProcessingTopology previous,
        RadarProcessingTopology current,
        RadarProcessingPartitionMigration migration)
    {
        ArgumentNullException.ThrowIfNull(migration);

        var sequence = ValidateTopologySequence(previous, current);
        if (!sequence.IsValid)
        {
            return sequence;
        }

        if (migration.ExpectedTopologyVersion != previous.Version)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.MigrationTopologyVersionMismatch,
                "Migration expected topology version must match the previous topology.");
        }

        if ((uint)migration.PartitionId >= (uint)previous.PartitionCount ||
            previous.GetShardIdForPartition(migration.PartitionId) != migration.SourceShardId ||
            current.GetShardIdForPartition(migration.PartitionId) != migration.TargetShardId)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.PartitionOwnerMismatch,
                "Accepted move ownership does not match previous and current topology snapshots.");
        }

        for (var partitionId = 0; partitionId < previous.PartitionCount; partitionId++)
        {
            if (partitionId == migration.PartitionId)
            {
                continue;
            }

            if (previous.GetShardIdForPartition(partitionId) != current.GetShardIdForPartition(partitionId))
            {
                return Invalid(
                    RadarProcessingRebalanceValidationError.AcceptedMoveChangedUnexpectedPartition,
                    "Accepted moves may change only the selected partition owner shard.");
            }
        }

        return RadarProcessingRebalanceValidationResult.Valid();
    }

    /// <summary>
    /// Validates that route and processing telemetry match the same topology snapshot.
}
