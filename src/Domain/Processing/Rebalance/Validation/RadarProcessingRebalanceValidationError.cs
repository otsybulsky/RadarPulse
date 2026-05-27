namespace RadarPulse.Domain.Processing;

public enum RadarProcessingRebalanceValidationError
{
    None = 0,
    NonMonotonicTopologyVersion,
    TopologyShapeMismatch,
    SourcePartitionMappingChanged,
    PartitionOwnerMismatch,
    AcceptedMoveChangedUnexpectedPartition,
    RouteTelemetryTopologyVersionMismatch,
    RouteTopologyVersionMismatch,
    RoutePartitionOwnerMismatch,
    RouteEventOwnershipMismatch,
    PressureSampleTelemetryMismatch,
    MissingTelemetry,
    UnexpectedRebalanceArtifacts,
    DecisionTopologyVersionMismatch,
    AcceptedDecisionMissingMigration,
    MigrationFailed,
    MigrationTopologyVersionMismatch,
    MissingStateHandoffValidation,
    StateHandoffValidationFailed
}
