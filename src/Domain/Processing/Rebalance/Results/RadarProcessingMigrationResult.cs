namespace RadarPulse.Domain.Processing;

/// <summary>
/// Result of validating and publishing a rebalance partition migration.
/// </summary>
public sealed class RadarProcessingMigrationResult
{
    private RadarProcessingMigrationResult(
        bool succeeded,
        RadarProcessingPartitionMigrationState state,
        RadarProcessingMigrationValidationResult validation,
        RadarProcessingPartitionMigration? migration,
        RadarProcessingTopologyVersion previousTopologyVersion,
        RadarProcessingTopologyVersion currentTopologyVersion,
        RadarProcessingTopologyMoveError topologyMoveError)
    {
        ArgumentNullException.ThrowIfNull(validation);

        if (!Enum.IsDefined(state) || state == RadarProcessingPartitionMigrationState.None)
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "Migration result state must be explicit.");
        }

        if (succeeded && state != RadarProcessingPartitionMigrationState.Published)
        {
            throw new ArgumentException("Successful migration results must be published.", nameof(state));
        }

        if (!succeeded && state == RadarProcessingPartitionMigrationState.Published)
        {
            throw new ArgumentException("Published migration results must be successful.", nameof(state));
        }

        if (succeeded && topologyMoveError != RadarProcessingTopologyMoveError.None)
        {
            throw new ArgumentException("Successful migration results must not carry topology move errors.", nameof(topologyMoveError));
        }

        Succeeded = succeeded;
        State = state;
        Validation = validation;
        Migration = migration;
        PreviousTopologyVersion = previousTopologyVersion;
        CurrentTopologyVersion = currentTopologyVersion;
        TopologyMoveError = topologyMoveError;
    }

    /// <summary>
    /// Indicates whether the topology move was published.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Final migration publication state.
    /// </summary>
    public RadarProcessingPartitionMigrationState State { get; }

    /// <summary>
    /// Validation evidence used before publication.
    /// </summary>
    public RadarProcessingMigrationValidationResult Validation { get; }

    /// <summary>
    /// Migration command associated with the result, when available.
    /// </summary>
    public RadarProcessingPartitionMigration? Migration { get; }

    /// <summary>
    /// Topology version before the publication attempt.
    /// </summary>
    public RadarProcessingTopologyVersion PreviousTopologyVersion { get; }

    /// <summary>
    /// Topology version after the publication attempt.
    /// </summary>
    public RadarProcessingTopologyVersion CurrentTopologyVersion { get; }

    /// <summary>
    /// Low-level topology move error, or none when not applicable.
    /// </summary>
    public RadarProcessingTopologyMoveError TopologyMoveError { get; }

    /// <summary>
    /// Creates a successful published migration result.
    /// </summary>
    public static RadarProcessingMigrationResult Published(
        RadarProcessingMigrationValidationResult validation,
        RadarProcessingTopologyMoveResult topologyMoveResult)
    {
        ArgumentNullException.ThrowIfNull(validation);
        ArgumentNullException.ThrowIfNull(topologyMoveResult);

        if (!validation.IsValid)
        {
            throw new ArgumentException("Published migration results require valid migration validation.", nameof(validation));
        }

        if (!topologyMoveResult.Succeeded)
        {
            throw new ArgumentException("Published migration results require a successful topology move.", nameof(topologyMoveResult));
        }

        return new RadarProcessingMigrationResult(
            succeeded: true,
            RadarProcessingPartitionMigrationState.Published,
            validation,
            validation.Migration,
            topologyMoveResult.PreviousTopology.Version,
            topologyMoveResult.CurrentTopology.Version,
            RadarProcessingTopologyMoveError.None);
    }

    /// <summary>
    /// Creates a result for a decision that could not become a migration.
    /// </summary>
    public static RadarProcessingMigrationResult RejectedDecision(
        RadarProcessingMigrationValidationResult validation) =>
        Rejected(
            RadarProcessingPartitionMigrationState.RejectedDecision,
            validation,
            validation.CurrentTopologyVersion,
            RadarProcessingTopologyMoveError.None);

    /// <summary>
    /// Creates a result for migration validation failure.
    /// </summary>
    public static RadarProcessingMigrationResult ValidationFailed(
        RadarProcessingMigrationValidationResult validation,
        RadarProcessingTopologyMoveError topologyMoveError = RadarProcessingTopologyMoveError.None) =>
        Rejected(
            RadarProcessingPartitionMigrationState.ValidationFailed,
            validation,
            validation.CurrentTopologyVersion,
            topologyMoveError);

    private static RadarProcessingMigrationResult Rejected(
        RadarProcessingPartitionMigrationState state,
        RadarProcessingMigrationValidationResult validation,
        RadarProcessingTopologyVersion currentTopologyVersion,
        RadarProcessingTopologyMoveError topologyMoveError) =>
        new(
            succeeded: false,
            state,
            validation,
            validation.Migration,
            currentTopologyVersion,
            currentTopologyVersion,
            topologyMoveError);
}
