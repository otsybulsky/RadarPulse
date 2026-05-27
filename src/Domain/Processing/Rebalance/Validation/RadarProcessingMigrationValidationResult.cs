namespace RadarPulse.Domain.Processing;

/// <summary>
/// Validation result for converting a rebalance decision to a migration.
/// </summary>
public sealed class RadarProcessingMigrationValidationResult
{
    private RadarProcessingMigrationValidationResult(
        bool isValid,
        RadarProcessingMigrationValidationError error,
        RadarProcessingTopologyVersion currentTopologyVersion,
        RadarProcessingPartitionMigration? migration)
    {
        if (isValid && error != RadarProcessingMigrationValidationError.None)
        {
            throw new ArgumentException("Valid migration validation results must not carry an error.", nameof(error));
        }

        if (!isValid && error == RadarProcessingMigrationValidationError.None)
        {
            throw new ArgumentException("Invalid migration validation results must carry an error.", nameof(error));
        }

        IsValid = isValid;
        Error = error;
        CurrentTopologyVersion = currentTopologyVersion;
        Migration = migration;
    }

    /// <summary>
    /// Indicates whether the migration can be published.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Validation error, or none when valid.
    /// </summary>
    public RadarProcessingMigrationValidationError Error { get; }

    /// <summary>
    /// Topology version observed during validation.
    /// </summary>
    public RadarProcessingTopologyVersion CurrentTopologyVersion { get; }

    /// <summary>
    /// Migration command when one could be derived.
    /// </summary>
    public RadarProcessingPartitionMigration? Migration { get; }

    /// <summary>
    /// Creates a valid migration validation result.
    /// </summary>
    public static RadarProcessingMigrationValidationResult Valid(
        RadarProcessingTopologyVersion currentTopologyVersion,
        RadarProcessingPartitionMigration migration)
    {
        ArgumentNullException.ThrowIfNull(migration);

        return new RadarProcessingMigrationValidationResult(
            isValid: true,
            RadarProcessingMigrationValidationError.None,
            currentTopologyVersion,
            migration);
    }

    /// <summary>
    /// Creates an invalid migration validation result.
    /// </summary>
    public static RadarProcessingMigrationValidationResult Invalid(
        RadarProcessingMigrationValidationError error,
        RadarProcessingTopologyVersion currentTopologyVersion,
        RadarProcessingPartitionMigration? migration = null) =>
        new(
            isValid: false,
            error,
            currentTopologyVersion,
            migration);
}
