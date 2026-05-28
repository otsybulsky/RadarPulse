using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceTelemetryContractTests
{
    [Fact]
    public void RecentValidationFailureCarriesErrorCodes()
    {
        var failure = new RadarProcessingRebalanceRecentValidationFailure(
            evaluationSequence: 9,
            RadarProcessingTopologyVersion.Initial,
            RadarProcessingRebalanceValidationError.MigrationFailed,
            RadarProcessingMigrationValidationError.StaleTopologyVersion,
            RadarProcessingStateHandoffValidationError.RawValueChecksumMismatch);

        Assert.Equal(9, failure.EvaluationSequence);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, failure.TopologyVersion);
        Assert.Equal(RadarProcessingRebalanceValidationError.MigrationFailed, failure.Error);
        Assert.Equal(RadarProcessingMigrationValidationError.StaleTopologyVersion, failure.MigrationError);
        Assert.Equal(RadarProcessingStateHandoffValidationError.RawValueChecksumMismatch, failure.HandoffError);
    }

    [Fact]
    public void RecentValidationFailureRejectsInvalidShape()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRecentValidationFailure(
                evaluationSequence: -1,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceValidationError.MigrationFailed));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRecentValidationFailure(
                evaluationSequence: 0,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceValidationError.None));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRecentValidationFailure(
                evaluationSequence: 0,
                RadarProcessingTopologyVersion.Initial,
                (RadarProcessingRebalanceValidationError)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRecentValidationFailure(
                evaluationSequence: 0,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceValidationError.MigrationFailed,
                migrationError: (RadarProcessingMigrationValidationError)255));
    }

    [Fact]
    public void RecentValidationFailureCanBeProjectedFromInvalidResult()
    {
        var validation = RadarProcessingRebalanceValidationResult.Invalid(
            RadarProcessingRebalanceValidationError.StateHandoffValidationFailed,
            "handoff failed",
            handoffError: RadarProcessingStateHandoffValidationError.ProcessingChecksumMismatch);

        var failure = RadarProcessingRebalanceRecentValidationFailure.FromResult(
            evaluationSequence: 3,
            RadarProcessingTopologyVersion.Initial,
            validation);

        Assert.Equal(3, failure.EvaluationSequence);
        Assert.Equal(validation.Error, failure.Error);
        Assert.Equal(validation.HandoffError, failure.HandoffError);
        Assert.Throws<ArgumentException>(() =>
            RadarProcessingRebalanceRecentValidationFailure.FromResult(
                evaluationSequence: 3,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceValidationResult.Valid()));
    }

}
