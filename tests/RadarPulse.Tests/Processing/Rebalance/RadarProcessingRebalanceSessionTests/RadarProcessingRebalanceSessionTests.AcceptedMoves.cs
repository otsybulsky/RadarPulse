using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceSessionTests
{
    [Fact]
    public void AcceptedRebalanceAfterFirstBatchAffectsSecondBatchRoute()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var session = CreateSession(universe);

        var first = session.Process(CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1]));

        Assert.True(first.ProcessingResult.IsValid);
        Assert.True(first.Validation.IsValid);
        Assert.NotNull(first.PressureSample);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, first.ProcessingResult.TopologyVersion);
        Assert.Equal(first.ProcessingResult.TopologyVersion, first.PressureSample.TopologyVersion);
        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, first.RebalanceDecision!.Kind);
        Assert.Equal(RadarProcessingRebalanceMoveKind.DirectHotRelief, first.RebalanceDecision.MoveKind);
        Assert.True(first.PublishedMigration);
        Assert.True(first.HandoffValidation!.IsValid);
        Assert.Equal(0, first.RebalanceDecision.PartitionId);
        Assert.Equal(1, first.TelemetrySummary.Counters.EvaluationCount);
        Assert.Equal(1, first.TelemetrySummary.Counters.AcceptedMoveCount);
        Assert.Equal(1, first.TelemetrySummary.Counters.DirectHotReliefMoveCount);
        Assert.Single(first.TelemetrySummary.RecentDecisions);
        Assert.Single(first.TelemetrySummary.RecentAcceptedMoves);
        Assert.Equal(1, session.CurrentTopology.GetShardIdForPartition(0));
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), session.CurrentTopology.Version);

        var second = session.Process(CreateEmptyBatch(universe.Version));
        var secondTelemetry = Assert.IsType<RadarProcessingTelemetry>(second.ProcessingResult.Telemetry);

        Assert.True(second.ProcessingResult.IsValid);
        Assert.True(second.Validation.IsValid);
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
        Assert.True(result.Validation.IsValid);
        Assert.True(result.HandoffValidation!.IsValid);
        Assert.Equal(1, result.RebalanceDecision.PartitionId);
        Assert.Equal(2, result.TelemetrySummary.Counters.EvaluationCount);
        Assert.Equal(1, result.TelemetrySummary.Counters.RejectedCandidateCount);
        Assert.Equal(1, result.TelemetrySummary.Counters.AcceptedMoveCount);
        Assert.Equal(1, result.TelemetrySummary.Counters.ColdEvacuationMoveCount);
        Assert.Contains(
            result.TelemetrySummary.SkippedReasonCounters,
            counter => counter.Reason == RadarProcessingRebalanceSkippedReason.DirectHotPartitionHasNoSafeTarget);
        Assert.Equal(1, session.CurrentTopology.GetShardIdForPartition(1));
        Assert.True(session.HotPartitionClassifier.GetPartition(0).IsIntrinsicHot);
    }
}
