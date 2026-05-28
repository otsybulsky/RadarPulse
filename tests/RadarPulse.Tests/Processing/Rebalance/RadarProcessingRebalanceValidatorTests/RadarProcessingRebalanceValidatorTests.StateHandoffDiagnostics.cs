using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceValidatorTests
{
    [Fact]
    public void InvalidStateHandoffIsReportedWithDiagnostics()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var options = CreateOptions(partitionCount: 4, shardCount: 2);
        var core = new RadarProcessingCore(universe, options);
        var processingResult = core.Process(CreateEmptyBatch(universe.Version));
        var telemetry = Assert.IsType<RadarProcessingTelemetry>(processingResult.Telemetry);
        var pressureSample = RadarProcessingPressureSample.FromTelemetry(telemetry);
        var manager = new RadarProcessingTopologyManager(universe, options);
        var previous = manager.Current;
        var decision = RadarProcessingRebalanceDecision.AcceptedMove(
            decisionId: 3,
            evaluationSequence: 1,
            previous.Version,
            pressureWindowSampleCount: 1,
            new RadarProcessingRebalanceCandidate(
                RadarProcessingRebalanceMoveKind.DirectHotRelief,
                partitionId: 0,
                sourceShardId: 0,
                targetShardId: 1,
                new RadarProcessingProjectedPressure(
                    new RadarProcessingPressureScore(5.0),
                    RadarProcessingPressureScore.Zero,
                    new RadarProcessingPressureScore(4.0),
                    new RadarProcessingPressureScore(1.0)),
                expectedRelief: 1.0));
        var migration = new RadarProcessingMigrationCoordinator(manager).Apply(decision);
        var before = new RadarProcessingPartitionStateSnapshot(
            partitionId: 0,
            shardId: 0,
            sourceIdStart: 0,
            sourceIdEndExclusive: 1,
            activeSourceCount: 1,
            processedEventCount: 1,
            processedPayloadValueCount: 1,
            rawValueChecksum: 1,
            new RadarProcessingPartitionStateChecksum(
                ProcessingChecksum: 1,
                LastMessageTimestampChecksum: 2,
                HandlerSnapshotChecksum: 0));
        var after = new RadarProcessingPartitionStateSnapshot(
            partitionId: 0,
            shardId: 1,
            sourceIdStart: 0,
            sourceIdEndExclusive: 1,
            activeSourceCount: 0,
            processedEventCount: 1,
            processedPayloadValueCount: 1,
            rawValueChecksum: 1,
            before.Checksum);
        var handoff = RadarProcessingStateHandoffValidator.Validate(before, after);

        var sessionResult = new RadarProcessingRebalanceSessionResult(
            processingResult,
            pressureSample,
            directHotReliefDecision: decision,
            coldEvacuationDecision: null,
            migration,
            handoff,
            manager.Current);

        AssertInvalid(
            sessionResult.Validation,
            RadarProcessingRebalanceValidationError.StateHandoffValidationFailed);
        Assert.Equal(
            RadarProcessingStateHandoffValidationError.ActiveSourceCountMismatch,
            sessionResult.Validation.HandoffError);
    }
}
