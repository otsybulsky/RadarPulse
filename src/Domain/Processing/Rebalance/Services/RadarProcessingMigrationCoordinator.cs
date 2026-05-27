namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingMigrationCoordinator
{
    private readonly RadarProcessingTopologyManager topologyManager;

    public RadarProcessingMigrationCoordinator(
        RadarProcessingTopologyManager topologyManager)
    {
        ArgumentNullException.ThrowIfNull(topologyManager);

        this.topologyManager = topologyManager;
    }

    public RadarProcessingMigrationResult Apply(
        RadarProcessingRebalanceDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        var validation = Validate(decision);
        if (!validation.IsValid)
        {
            return validation.Error == RadarProcessingMigrationValidationError.DecisionNotAcceptedMove ||
                   validation.Error == RadarProcessingMigrationValidationError.MissingCandidate
                ? RadarProcessingMigrationResult.RejectedDecision(validation)
                : RadarProcessingMigrationResult.ValidationFailed(validation);
        }

        var moveResult = topologyManager.MovePartition(validation.Migration!.ToTopologyMoveRequest());
        if (!moveResult.Succeeded)
        {
            return RadarProcessingMigrationResult.ValidationFailed(
                RadarProcessingMigrationValidationResult.Invalid(
                    MapTopologyMoveError(moveResult.Error),
                    topologyManager.Current.Version,
                    validation.Migration),
                moveResult.Error);
        }

        return RadarProcessingMigrationResult.Published(validation, moveResult);
    }

    public RadarProcessingMigrationValidationResult Validate(
        RadarProcessingRebalanceDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        var current = topologyManager.Current;
        if (!decision.HasAcceptedMove)
        {
            return RadarProcessingMigrationValidationResult.Invalid(
                RadarProcessingMigrationValidationError.DecisionNotAcceptedMove,
                current.Version);
        }

        if (decision.Candidate is null)
        {
            return RadarProcessingMigrationValidationResult.Invalid(
                RadarProcessingMigrationValidationError.MissingCandidate,
                current.Version);
        }

        var migration = RadarProcessingPartitionMigration.FromDecision(decision);
        var validationError = ValidateMigrationShape(current, migration);
        return validationError == RadarProcessingMigrationValidationError.None
            ? RadarProcessingMigrationValidationResult.Valid(current.Version, migration)
            : RadarProcessingMigrationValidationResult.Invalid(
                validationError,
                current.Version,
                migration);
    }

    private static RadarProcessingMigrationValidationError ValidateMigrationShape(
        RadarProcessingTopology current,
        RadarProcessingPartitionMigration migration)
    {
        if (migration.ExpectedTopologyVersion != current.Version)
        {
            return RadarProcessingMigrationValidationError.StaleTopologyVersion;
        }

        if ((uint)migration.PartitionId >= (uint)current.PartitionCount)
        {
            return RadarProcessingMigrationValidationError.PartitionIdOutOfRange;
        }

        if ((uint)migration.SourceShardId >= (uint)current.ShardCount)
        {
            return RadarProcessingMigrationValidationError.SourceShardIdOutOfRange;
        }

        if ((uint)migration.TargetShardId >= (uint)current.ShardCount)
        {
            return RadarProcessingMigrationValidationError.TargetShardIdOutOfRange;
        }

        if (migration.SourceShardId == migration.TargetShardId)
        {
            return RadarProcessingMigrationValidationError.NoOpMove;
        }

        return current.GetShardIdForPartition(migration.PartitionId) == migration.SourceShardId
            ? RadarProcessingMigrationValidationError.None
            : RadarProcessingMigrationValidationError.SourceShardOwnershipMismatch;
    }

    private static RadarProcessingMigrationValidationError MapTopologyMoveError(
        RadarProcessingTopologyMoveError error) =>
        error switch
        {
            RadarProcessingTopologyMoveError.None => RadarProcessingMigrationValidationError.None,
            RadarProcessingTopologyMoveError.StaleTopologyVersion =>
                RadarProcessingMigrationValidationError.StaleTopologyVersion,
            RadarProcessingTopologyMoveError.PartitionIdOutOfRange =>
                RadarProcessingMigrationValidationError.PartitionIdOutOfRange,
            RadarProcessingTopologyMoveError.SourceShardIdOutOfRange =>
                RadarProcessingMigrationValidationError.SourceShardIdOutOfRange,
            RadarProcessingTopologyMoveError.TargetShardIdOutOfRange =>
                RadarProcessingMigrationValidationError.TargetShardIdOutOfRange,
            RadarProcessingTopologyMoveError.NoOpMove =>
                RadarProcessingMigrationValidationError.NoOpMove,
            RadarProcessingTopologyMoveError.SourceShardOwnershipMismatch =>
                RadarProcessingMigrationValidationError.SourceShardOwnershipMismatch,
            _ => throw new ArgumentOutOfRangeException(nameof(error), error, "Unsupported topology move error.")
        };
}
