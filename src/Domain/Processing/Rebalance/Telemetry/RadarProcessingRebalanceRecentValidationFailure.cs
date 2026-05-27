namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingRebalanceRecentValidationFailure
{
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

    public long EvaluationSequence { get; }

    public RadarProcessingTopologyVersion TopologyVersion { get; }

    public RadarProcessingRebalanceValidationError Error { get; }

    public RadarProcessingMigrationValidationError MigrationError { get; }

    public RadarProcessingStateHandoffValidationError HandoffError { get; }

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
