namespace RadarPulse.Domain.Processing;

public static class RadarProcessingRebalanceValidator
{
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

    public static RadarProcessingRebalanceValidationResult ValidateRouteTelemetry(
        RadarProcessingBatchRoute route,
        RadarProcessingTelemetry telemetry,
        RadarProcessingTopology topology)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(telemetry);
        ArgumentNullException.ThrowIfNull(topology);

        if (route.TopologyVersion != telemetry.TopologyVersion)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.RouteTelemetryTopologyVersionMismatch,
                "Route topology version must match telemetry topology version.");
        }

        if (route.PartitionCount != topology.PartitionCount ||
            route.ShardCount != topology.ShardCount ||
            telemetry.PartitionCount != topology.PartitionCount ||
            telemetry.ShardCount != topology.ShardCount)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.TopologyShapeMismatch,
                "Route, telemetry, and topology shapes must match.");
        }

        for (var partitionId = 0; partitionId < topology.PartitionCount; partitionId++)
        {
            var expectedShardId = topology.GetShardIdForPartition(partitionId);
            var routePartition = route.GetPartition(partitionId);
            var telemetryPartition = telemetry.Partitions[partitionId];

            if (routePartition.PartitionId != partitionId ||
                telemetryPartition.PartitionId != partitionId ||
                routePartition.ShardId != expectedShardId ||
                telemetryPartition.ShardId != routePartition.ShardId)
            {
                return Invalid(
                    RadarProcessingRebalanceValidationError.RoutePartitionOwnerMismatch,
                    "Route and telemetry partition ownership must match the topology snapshot.");
            }
        }

        foreach (var routedEvent in route.RoutedEvents.Span)
        {
            var expectedPartitionId = topology.GetPartitionIdForSource(routedEvent.SourceId);
            var expectedShardId = topology.GetShardIdForSource(routedEvent.SourceId);

            if (routedEvent.PartitionId != expectedPartitionId ||
                routedEvent.ShardId != expectedShardId)
            {
                return Invalid(
                    RadarProcessingRebalanceValidationError.RouteEventOwnershipMismatch,
                    "Routed event ownership must match the topology snapshot used for validation.");
            }
        }

        if (route.TopologyVersion != topology.Version ||
            telemetry.TopologyVersion != topology.Version)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.RouteTopologyVersionMismatch,
                "Route and telemetry topology versions must match the topology snapshot.");
        }

        return RadarProcessingRebalanceValidationResult.Valid();
    }

    public static RadarProcessingRebalanceValidationResult ValidatePressureSample(
        RadarProcessingPressureSample sample,
        RadarProcessingTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(telemetry);

        if (sample.TopologyVersion != telemetry.TopologyVersion ||
            sample.BatchMetrics != telemetry.BatchMetrics ||
            sample.PartitionCount != telemetry.PartitionCount ||
            sample.ShardCount != telemetry.ShardCount)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.PressureSampleTelemetryMismatch,
                "Pressure sample summary must match the telemetry it was created from.");
        }

        for (var partitionId = 0; partitionId < sample.PartitionCount; partitionId++)
        {
            var samplePartition = sample.Partitions[partitionId];
            var telemetryPartition = telemetry.Partitions[partitionId];
            if (samplePartition.PartitionId != telemetryPartition.PartitionId ||
                samplePartition.ShardId != telemetryPartition.ShardId ||
                samplePartition.Metrics != telemetryPartition.Metrics)
            {
                return Invalid(
                    RadarProcessingRebalanceValidationError.PressureSampleTelemetryMismatch,
                    "Pressure sample partition summary must match telemetry.");
            }
        }

        for (var shardId = 0; shardId < sample.ShardCount; shardId++)
        {
            var sampleShard = sample.Shards[shardId];
            var telemetryShard = telemetry.Shards[shardId];
            if (sampleShard.ShardId != telemetryShard.ShardId ||
                sampleShard.PartitionCount != telemetryShard.PartitionCount ||
                sampleShard.ActivePartitionCount != telemetryShard.ActivePartitionCount ||
                sampleShard.Metrics != telemetryShard.Metrics)
            {
                return Invalid(
                    RadarProcessingRebalanceValidationError.PressureSampleTelemetryMismatch,
                    "Pressure sample shard summary must match telemetry.");
            }
        }

        return RadarProcessingRebalanceValidationResult.Valid();
    }

    public static RadarProcessingRebalanceValidationResult ValidateSessionResult(
        RadarProcessingRebalanceSessionResult result,
        RadarProcessingTopology currentTopology)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(currentTopology);

        if (!result.ProcessingResult.IsValid)
        {
            return result.PressureSample is null &&
                   result.RebalanceDecision is null &&
                   result.MigrationResult is null &&
                   result.HandoffValidation is null
                ? RadarProcessingRebalanceValidationResult.Valid()
                : Invalid(
                    RadarProcessingRebalanceValidationError.UnexpectedRebalanceArtifacts,
                    "Invalid processing results must not carry rebalance artifacts.");
        }

        var telemetry = result.ProcessingResult.Telemetry;
        if (telemetry is null)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.MissingTelemetry,
                "Valid rebalance processing results must carry partitioned telemetry.");
        }

        if (result.ProcessingResult.TopologyVersion != telemetry.TopologyVersion)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.RouteTelemetryTopologyVersionMismatch,
                "Processing result topology version must match telemetry topology version.");
        }

        if (result.PressureSample is null)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.PressureSampleTelemetryMismatch,
                "Valid rebalance processing results must carry a pressure sample.");
        }

        var pressureValidation = ValidatePressureSample(result.PressureSample, telemetry);
        if (!pressureValidation.IsValid)
        {
            return pressureValidation;
        }

        var decision = result.RebalanceDecision;
        if (decision is null)
        {
            return result.MigrationResult is null && result.HandoffValidation is null
                ? RadarProcessingRebalanceValidationResult.Valid()
                : Invalid(
                    RadarProcessingRebalanceValidationError.UnexpectedRebalanceArtifacts,
                    "Missing rebalance decisions must not carry migration or handoff artifacts.");
        }

        if (decision.TopologyVersion != result.ProcessingResult.TopologyVersion ||
            decision.TopologyVersion != result.PressureSample.TopologyVersion)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.DecisionTopologyVersionMismatch,
                "Rebalance decision topology version must match the processed pressure sample.");
        }

        if (result.HandoffValidation is not null && !result.HandoffValidation.IsValid)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.StateHandoffValidationFailed,
                "State handoff validation failed.",
                handoffError: result.HandoffValidation.Error);
        }

        if (!decision.HasAcceptedMove)
        {
            return result.MigrationResult is null
                ? RadarProcessingRebalanceValidationResult.Valid()
                : Invalid(
                    RadarProcessingRebalanceValidationError.UnexpectedRebalanceArtifacts,
                    "Non-accepted rebalance decisions must not carry migration results.");
        }

        if (result.MigrationResult is null)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.AcceptedDecisionMissingMigration,
                "Accepted rebalance decisions must carry a migration result.");
        }

        if (!result.MigrationResult.Succeeded)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.MigrationFailed,
                "Accepted rebalance migration failed.",
                migrationError: result.MigrationResult.Validation.Error);
        }

        if (result.MigrationResult.PreviousTopologyVersion != decision.TopologyVersion ||
            result.MigrationResult.CurrentTopologyVersion != currentTopology.Version)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.MigrationTopologyVersionMismatch,
                "Migration topology versions must match the decision and current topology.");
        }

        if (result.HandoffValidation is null)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.MissingStateHandoffValidation,
                "Published rebalance moves must carry state handoff validation.");
        }

        return RadarProcessingRebalanceValidationResult.Valid();
    }

    private static RadarProcessingRebalanceValidationResult Invalid(
        RadarProcessingRebalanceValidationError error,
        string message,
        RadarProcessingMigrationValidationError migrationError = RadarProcessingMigrationValidationError.None,
        RadarProcessingStateHandoffValidationError handoffError = RadarProcessingStateHandoffValidationError.None) =>
        RadarProcessingRebalanceValidationResult.Invalid(
            error,
            message,
            migrationError,
            handoffError);
}
