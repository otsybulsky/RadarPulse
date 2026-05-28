using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceValidatorTests
{
    [Fact]
    public void InvalidAcceptedMoveOwnershipIsRejected()
    {
        var manager = CreateManager(sourceCount: 4, partitionCount: 4, shardCount: 2);
        var previous = manager.Current;
        manager.MovePartition(
            new RadarProcessingTopologyMoveRequest(
                previous.Version,
                partitionId: 0,
                sourceShardId: 0,
                targetShardId: 1));
        var wrongMove = new RadarProcessingPartitionMigration(
            decisionId: 2,
            RadarProcessingRebalanceMoveKind.DirectHotRelief,
            previous.Version,
            partitionId: 1,
            sourceShardId: 0,
            targetShardId: 1);

        var result = RadarProcessingRebalanceValidator.ValidateAcceptedMove(
            previous,
            manager.Current,
            wrongMove);

        AssertInvalid(result, RadarProcessingRebalanceValidationError.PartitionOwnerMismatch);
    }

    [Fact]
    public void OffProfileSkipsSessionReadSideValidation()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = new RadarProcessingCore(universe, CreateOptions(partitionCount: 4, shardCount: 2));
        var processingResult = core.Process(CreateEmptyBatch(universe.Version));

        var result = new RadarProcessingRebalanceSessionResult(
            processingResult,
            pressureSample: null,
            directHotReliefDecision: null,
            coldEvacuationDecision: null,
            migrationResult: null,
            handoffValidation: null,
            currentTopology: core.Topology,
            validationProfile: RadarProcessingValidationProfile.Off);

        Assert.Equal(RadarProcessingValidationProfile.Off, result.ValidationProfile);
        Assert.True(result.Validation.IsValid);
    }

    [Theory]
    [InlineData(RadarProcessingValidationProfile.Diagnostic)]
    [InlineData(RadarProcessingValidationProfile.Benchmark)]
    public void DiagnosticProfilesPreserveSessionReadSideValidation(
        RadarProcessingValidationProfile validationProfile)
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = new RadarProcessingCore(universe, CreateOptions(partitionCount: 4, shardCount: 2));
        var processingResult = core.Process(CreateEmptyBatch(universe.Version));

        var result = new RadarProcessingRebalanceSessionResult(
            processingResult,
            pressureSample: null,
            directHotReliefDecision: null,
            coldEvacuationDecision: null,
            migrationResult: null,
            handoffValidation: null,
            currentTopology: core.Topology,
            validationProfile: validationProfile);

        Assert.Equal(validationProfile, result.ValidationProfile);
        AssertInvalid(result.Validation, RadarProcessingRebalanceValidationError.PressureSampleTelemetryMismatch);
    }

    [Fact]
    public void EssentialProfileReportsStateHandoffFailureWithoutPressureDiagnostics()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = new RadarProcessingCore(universe, CreateOptions(partitionCount: 4, shardCount: 2));
        var processingResult = core.Process(CreateEmptyBatch(universe.Version));
        var handoff = CreateInvalidStateHandoff();

        var result = new RadarProcessingRebalanceSessionResult(
            processingResult,
            pressureSample: null,
            directHotReliefDecision: CreateAcceptedDecision(core.Topology.Version),
            coldEvacuationDecision: null,
            migrationResult: null,
            handoffValidation: handoff,
            currentTopology: core.Topology,
            validationProfile: RadarProcessingValidationProfile.Essential);

        Assert.Equal(RadarProcessingValidationProfile.Essential, result.ValidationProfile);
        AssertInvalid(result.Validation, RadarProcessingRebalanceValidationError.StateHandoffValidationFailed);
        Assert.Equal(
            RadarProcessingStateHandoffValidationError.ActiveSourceCountMismatch,
            result.Validation.HandoffError);
    }
}
