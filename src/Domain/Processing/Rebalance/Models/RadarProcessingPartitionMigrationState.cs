namespace RadarPulse.Domain.Processing;

/// <summary>
/// Publication state for a rebalance partition migration.
/// </summary>
public enum RadarProcessingPartitionMigrationState
{
    /// <summary>
    /// No migration state.
    /// </summary>
    None = 0,

    /// <summary>
    /// The decision was not eligible to become a migration.
    /// </summary>
    RejectedDecision,

    /// <summary>
    /// Migration validation failed before or during topology publication.
    /// </summary>
    ValidationFailed,

    /// <summary>
    /// The topology move was published.
    /// </summary>
    Published
}
