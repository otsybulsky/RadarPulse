using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingRebalanceValidatorTests
{
    [Fact]
    public void ValidTopologySequencePasses()
    {
        var manager = CreateManager(sourceCount: 4, partitionCount: 4, shardCount: 2);
        var previous = manager.Current;
        var move = new RadarProcessingPartitionMigration(
            decisionId: 1,
            RadarProcessingRebalanceMoveKind.DirectHotRelief,
            previous.Version,
            partitionId: 0,
            sourceShardId: 0,
            targetShardId: 1);

        manager.MovePartition(move.ToTopologyMoveRequest());

        var sequence = RadarProcessingRebalanceValidator.ValidateTopologySequence(previous, manager.Current);
        var acceptedMove = RadarProcessingRebalanceValidator.ValidateAcceptedMove(previous, manager.Current, move);

        Assert.True(sequence.IsValid);
        Assert.True(acceptedMove.IsValid);
    }

    [Fact]
    public void NonMonotonicTopologyVersionIsRejected()
    {
        var manager = CreateManager(sourceCount: 4, partitionCount: 4, shardCount: 2);
        var previous = manager.Current;
        manager.MovePartition(
            new RadarProcessingTopologyMoveRequest(
                previous.Version,
                partitionId: 0,
                sourceShardId: 0,
                targetShardId: 1));

        var result = RadarProcessingRebalanceValidator.ValidateTopologySequence(manager.Current, previous);

        AssertInvalid(result, RadarProcessingRebalanceValidationError.NonMonotonicTopologyVersion);
    }

    [Fact]
    public void MixedRouteAndTelemetryTopologyVersionsAreRejected()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var options = CreateOptions(partitionCount: 4, shardCount: 2);
        var core = new RadarProcessingCore(universe, options);
        var manager = new RadarProcessingTopologyManager(universe, options);
        var batch = CreateEightBitBatch(universe.Version, [0, 1]);
        var telemetry = Assert.IsType<RadarProcessingTelemetry>(core.Process(batch).Telemetry);

        manager.MovePartition(
            new RadarProcessingTopologyMoveRequest(
                manager.Current.Version,
                partitionId: 0,
                sourceShardId: 0,
                targetShardId: 1));
        var route = new RadarProcessingBatchRouter(manager.Current).Route(batch);

        var result = RadarProcessingRebalanceValidator.ValidateRouteTelemetry(
            route,
            telemetry,
            manager.Current);

        AssertInvalid(result, RadarProcessingRebalanceValidationError.RouteTelemetryTopologyVersionMismatch);
    }

    [Fact]
    public void PartitionOwnerMismatchIsRejected()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var options = CreateOptions(partitionCount: 4, shardCount: 2);
        var core = new RadarProcessingCore(universe, options);
        var manager = new RadarProcessingTopologyManager(universe, options);
        var batch = CreateEightBitBatch(universe.Version, [0, 1]);
        var route = new RadarProcessingBatchRouter(manager.Current).Route(batch);
        var telemetry = Assert.IsType<RadarProcessingTelemetry>(core.Process(batch).Telemetry);

        manager.MovePartition(
            new RadarProcessingTopologyMoveRequest(
                manager.Current.Version,
                partitionId: 0,
                sourceShardId: 0,
                targetShardId: 1));

        var result = RadarProcessingRebalanceValidator.ValidateRouteTelemetry(
            route,
            telemetry,
            manager.Current);

        AssertInvalid(result, RadarProcessingRebalanceValidationError.RoutePartitionOwnerMismatch);
    }

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

    private static void AssertInvalid(
        RadarProcessingRebalanceValidationResult result,
        RadarProcessingRebalanceValidationError expectedError)
    {
        Assert.False(result.IsValid);
        Assert.Equal(expectedError, result.Error);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    private static RadarProcessingRebalanceDecision CreateAcceptedDecision(
        RadarProcessingTopologyVersion topologyVersion) =>
        RadarProcessingRebalanceDecision.AcceptedMove(
            decisionId: 10,
            evaluationSequence: 1,
            topologyVersion,
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

    private static RadarProcessingStateHandoffValidationResult CreateInvalidStateHandoff()
    {
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

        return RadarProcessingStateHandoffValidator.Validate(before, after);
    }

    private static RadarProcessingTopologyManager CreateManager(
        int sourceCount,
        int partitionCount,
        int shardCount) =>
        new(
            CreateUniverse(sourceCount),
            CreateOptions(partitionCount, shardCount));

    private static RadarProcessingCoreOptions CreateOptions(
        int partitionCount,
        int shardCount) =>
        new(
            RadarProcessingExecutionMode.PartitionedBarrier,
            partitionCount,
            shardCount);

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarEventBatch CreateEmptyBatch(SourceUniverseVersion sourceUniverseVersion) =>
        new(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            Array.Empty<RadarStreamEvent>(),
            Array.Empty<byte>());

    private static RadarEventBatch CreateEightBitBatch(
        SourceUniverseVersion sourceUniverseVersion,
        int[] sourceIds)
    {
        var events = new RadarStreamEvent[sourceIds.Length];
        var payload = new byte[sourceIds.Length];

        for (var i = 0; i < sourceIds.Length; i++)
        {
            events[i] = CreateEvent(sourceIds[i], messageTimestampUtcTicks: 100 + i, payloadOffset: i);
            payload[i] = (byte)(i + 1);
        }

        return new RadarEventBatch(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            events,
            payload);
    }

    private static RadarStreamEvent CreateEvent(
        int sourceId,
        long messageTimestampUtcTicks,
        int payloadOffset) =>
        new(
            sourceId,
            radarOrdinal: 0,
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks,
            sourceRecord: 1,
            sourceMessage: 1,
            radialSequence: payloadOffset,
            elevationSlot: 0,
            azimuthBucket: (ushort)sourceId,
            rangeBand: 0,
            momentId: 0,
            gateStart: 0,
            gateCount: 1,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payloadOffset: payloadOffset,
            payloadLength: 1);
}
