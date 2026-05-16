using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingRebalanceSessionTests
{
    [Fact]
    public void AcceptedRebalanceAfterFirstBatchAffectsSecondBatchRoute()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var session = CreateSession(universe);

        var first = session.Process(CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1]));

        Assert.True(first.ProcessingResult.IsValid);
        Assert.NotNull(first.PressureSample);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, first.ProcessingResult.TopologyVersion);
        Assert.Equal(first.ProcessingResult.TopologyVersion, first.PressureSample.TopologyVersion);
        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, first.RebalanceDecision!.Kind);
        Assert.Equal(RadarProcessingRebalanceMoveKind.DirectHotRelief, first.RebalanceDecision.MoveKind);
        Assert.True(first.PublishedMigration);
        Assert.True(first.HandoffValidation!.IsValid);
        Assert.Equal(0, first.RebalanceDecision.PartitionId);
        Assert.Equal(1, session.CurrentTopology.GetShardIdForPartition(0));
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), session.CurrentTopology.Version);

        var second = session.Process(CreateEmptyBatch(universe.Version));
        var secondTelemetry = Assert.IsType<RadarProcessingTelemetry>(second.ProcessingResult.Telemetry);

        Assert.True(second.ProcessingResult.IsValid);
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), second.ProcessingResult.TopologyVersion);
        Assert.Equal(second.ProcessingResult.TopologyVersion, secondTelemetry.TopologyVersion);
        Assert.Equal(1, secondTelemetry.Partitions[0].ShardId);
    }

    [Fact]
    public void ColdEvacuationRunsWhenDirectHotReliefCannotMoveSafely()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var session = CreateSession(universe);
        session.HotPartitionClassifier.ClassifyQuarantined(
            partitionId: 1,
            shardId: 0,
            evaluationSequence: 0);

        var result = session.Process(CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 0, 0, 0, 0, 1]));

        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, result.DirectHotReliefDecision!.Kind);
        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, result.ColdEvacuationDecision!.Kind);
        Assert.Equal(RadarProcessingRebalanceMoveKind.ColdEvacuation, result.RebalanceDecision!.MoveKind);
        Assert.True(result.PublishedMigration);
        Assert.True(result.HandoffValidation!.IsValid);
        Assert.Equal(1, result.RebalanceDecision.PartitionId);
        Assert.Equal(1, session.CurrentTopology.GetShardIdForPartition(1));
        Assert.True(session.HotPartitionClassifier.GetPartition(0).IsIntrinsicHot);
    }

    [Fact]
    public void InvalidProcessingResultDoesNotEvaluateRebalance()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var session = CreateSession(universe);

        var result = session.Process(CreateEmptyBatch(new SourceUniverseVersion(2)));

        Assert.False(result.ProcessingResult.IsValid);
        Assert.Null(result.PressureSample);
        Assert.Null(result.DirectHotReliefDecision);
        Assert.Null(result.ColdEvacuationDecision);
        Assert.Null(result.RebalanceDecision);
        Assert.Null(result.MigrationResult);
        Assert.Equal(0, session.PressureWindow.SampleCount);
        Assert.Equal(0, session.PolicyState.EvaluationSequence);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, session.CurrentTopology.Version);
    }

    [Fact]
    public void SequentialCoreIsRejectedForRebalanceSession()
    {
        var core = new RadarProcessingCore(CreateUniverse(sourceCount: 1));

        Assert.Throws<ArgumentException>(() => new RadarProcessingRebalanceSession(core));
    }

    private static RadarProcessingRebalanceSession CreateSession(RadarSourceUniverse universe)
    {
        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount: universe.SourceCount,
                shardCount: 2));

        return new RadarProcessingRebalanceSession(
            core,
            new RadarProcessingPressureOptions(
                eventWeight: 1.0,
                payloadValueWeight: 0.0,
                rawValueChecksumWeight: 0.0),
            new RadarProcessingPressureWindow(
                new RadarProcessingPressureWindowOptions(
                    sampleCapacity: 2,
                    minimumSampleCount: 1,
                    coldThreshold: 0.0,
                    warmExitThreshold: 4.0,
                    warmEnterThreshold: 4.5,
                    hotExitThreshold: 4.75,
                    hotEnterThreshold: 5.0,
                    superHotExitThreshold: 9.0,
                    superHotEnterThreshold: 10.0)),
            new RadarProcessingRebalancePolicyState(
                universe.SourceCount,
                shardCount: 2,
                new RadarProcessingRebalanceOptions(
                    budgetWindowEvaluationCount: 4,
                    globalMoveBudgetPerWindow: 4,
                    sourceShardMoveBudgetPerWindow: 4,
                    targetShardReceiveBudgetPerWindow: 4,
                    minimumPartitionResidencyEvaluations: 0,
                    partitionMoveCooldownEvaluations: 0,
                    sourceShardMoveCooldownEvaluations: 0,
                    targetShardReceiveCooldownEvaluations: 0,
                    minimumProjectedBenefit: 0.05)));
    }

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
            events[i] = CreateEvent(
                sourceIds[i],
                messageTimestampUtcTicks: 100 + i,
                payloadOffset: i);
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
