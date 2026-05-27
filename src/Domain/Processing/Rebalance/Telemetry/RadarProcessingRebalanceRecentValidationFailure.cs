namespace RadarPulse.Domain.Processing;

/// <summary>
/// Retained compact detail for a recent rebalance validation failure.
/// </summary>
public sealed record RadarProcessingRebalanceRecentValidationFailure
{
    /// <summary>
    /// Creates a retained validation failure entry.
    /// </summary>
    public RadarProcessingRebalanceRecentValidationFailure(
        long evaluationSequence,
        RadarProcessingTopologyVersion topologyVersion,
        RadarProcessingRebalanceValidationError error,
        RadarProcessingMigrationValidationError migrationError = RadarProcessingMigrationValidationError.None,
        RadarProcessingStateHandoffValidationError handoffError = RadarProcessingStateHandoffValidationError.None)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(evaluationSequence);

        if (!Enum.IsDefined(error) || error == RadarProcessingRebalanceValidationError.None)
        {
            throw new ArgumentOutOfRangeException(nameof(error), error, "Validation failure must carry an explicit error.");
        }

        if (!Enum.IsDefined(migrationError))
        {
            throw new ArgumentOutOfRangeException(nameof(migrationError));
        }

        if (!Enum.IsDefined(handoffError))
        {
            throw new ArgumentOutOfRangeException(nameof(handoffError));
        }

        EvaluationSequence = evaluationSequence;
        TopologyVersion = topologyVersion;
        Error = error;
        MigrationError = migrationError;
        HandoffError = handoffError;
    }

    /// <summary>
    /// Policy evaluation sequence that produced the failure.
    /// </summary>
    public long EvaluationSequence { get; }

    /// <summary>
    /// Topology version associated with the failed validation.
    /// </summary>
    public RadarProcessingTopologyVersion TopologyVersion { get; }

    /// <summary>
    /// High-level validation error.
    /// </summary>
    public RadarProcessingRebalanceValidationError Error { get; }

    /// <summary>
    /// Migration-specific validation error, when relevant.
    /// </summary>
    public RadarProcessingMigrationValidationError MigrationError { get; }

    /// <summary>
    /// Handoff-specific validation error, when relevant.
    /// </summary>
    public RadarProcessingStateHandoffValidationError HandoffError { get; }

    /// <summary>
    /// Creates retained failure detail from a validation result.
    /// </summary>
    public static RadarProcessingRebalanceRecentValidationFailure FromResult(
        long evaluationSequence,
        RadarProcessingTopologyVersion topologyVersion,
        RadarProcessingRebalanceValidationResult validation)
    {
        ArgumentNullException.ThrowIfNull(validation);

        if (validation.IsValid)
        {
            throw new ArgumentException("Recent validation failure requires an invalid validation result.", nameof(validation));
        }

        return new RadarProcessingRebalanceRecentValidationFailure(
            evaluationSequence,
            topologyVersion,
            validation.Error,
            validation.MigrationError,
            validation.HandoffError);
    }
}
