namespace RadarPulse.Domain.Processing;

public static partial class RadarProcessingRebalanceValidator
{
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

    /// <summary>
    /// Validates that a pressure sample faithfully summarizes processing telemetry.
    /// </summary>
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

    /// <summary>
    /// Validates a complete rebalance session result using the diagnostic profile.
}
