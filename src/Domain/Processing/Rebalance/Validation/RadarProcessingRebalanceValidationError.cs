namespace RadarPulse.Domain.Processing;

/// <summary>
/// Contract validation errors for rebalance topology, telemetry, and session artifacts.
/// </summary>
public enum RadarProcessingRebalanceValidationError
{
    /// <summary>
    /// No validation error.
    /// </summary>
    None = 0,

    /// <summary>
    /// A published topology did not advance the previous topology version.
    /// </summary>
    NonMonotonicTopologyVersion,

    /// <summary>
    /// Topology, route, telemetry, or pressure shapes do not match.
    /// </summary>
    TopologyShapeMismatch,

    /// <summary>
    /// Source-to-partition ranges changed across a rebalance move.
    /// </summary>
    SourcePartitionMappingChanged,

    /// <summary>
    /// A partition owner is missing, out of range, or inconsistent.
    /// </summary>
    PartitionOwnerMismatch,

    /// <summary>
    /// An accepted move changed a partition other than the selected one.
    /// </summary>
    AcceptedMoveChangedUnexpectedPartition,

    /// <summary>
    /// Route and telemetry topology versions differ.
    /// </summary>
    RouteTelemetryTopologyVersionMismatch,

    /// <summary>
    /// Route or telemetry was produced from a different topology snapshot.
    /// </summary>
    RouteTopologyVersionMismatch,

    /// <summary>
    /// Route or telemetry partition ownership does not match topology.
    /// </summary>
    RoutePartitionOwnerMismatch,

    /// <summary>
    /// A routed event does not match topology ownership for its source id.
    /// </summary>
    RouteEventOwnershipMismatch,

    /// <summary>
    /// A pressure sample does not match the telemetry it summarizes.
    /// </summary>
    PressureSampleTelemetryMismatch,

    /// <summary>
    /// A valid processing result was missing telemetry needed for rebalance.
    /// </summary>
    MissingTelemetry,

    /// <summary>
    /// Rebalance artifacts were present for a result state that should not carry them.
    /// </summary>
    UnexpectedRebalanceArtifacts,

    /// <summary>
    /// A rebalance decision topology version did not match the processed sample.
    /// </summary>
    DecisionTopologyVersionMismatch,

    /// <summary>
    /// An accepted move decision did not carry a migration result.
    /// </summary>
    AcceptedDecisionMissingMigration,

    /// <summary>
    /// A migration result failed for an accepted decision.
    /// </summary>
    MigrationFailed,

    /// <summary>
    /// Migration topology versions did not match the decision or current topology.
    /// </summary>
    MigrationTopologyVersionMismatch,

    /// <summary>
    /// A published move did not carry handoff validation evidence.
    /// </summary>
    MissingStateHandoffValidation,

    /// <summary>
    /// State handoff validation reported a mismatch.
    /// </summary>
    StateHandoffValidationFailed
}
