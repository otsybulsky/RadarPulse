namespace RadarPulse.Domain.Processing;

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

    public bool IsValid { get; }

    public RadarProcessingMigrationValidationError Error { get; }

    public RadarProcessingTopologyVersion CurrentTopologyVersion { get; }

    public RadarProcessingPartitionMigration? Migration { get; }

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
