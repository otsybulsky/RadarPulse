using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingDurableRebalanceSessionTests
{
    [Fact]
    public async Task DurableRebalanceRecomputesLaterCompletedDeltaAfterTopologyMove()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var reference = CreateSession(universe);
        var durable = CreateSession(universe);
        var batches = new[]
        {
            CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1]),
            CreateEmptyBatch(universe.Version)
        };
        var referenceResults = batches.Select(batch => reference.Process(batch)).ToArray();
        var queue = new RadarProcessingDurableEnvelopeQueue();
        await using var session = new RadarProcessingDurableRebalanceSession(durable, queue);

        queue.Accept(BatchId("batch-0"), batches[0]);
        queue.Accept(BatchId("batch-1"), batches[1]);

        var first = queue.ClaimNext("worker-a").ClaimedEnvelope!;
        var second = queue.ClaimNext("worker-b").ClaimedEnvelope!;

        await session.ProcessClaimedAsync(second);
        Assert.Empty(session.CommitReady());

        await session.ProcessClaimedAsync(first);
        var published = session.CommitReady();
        var result = session.CreateResult();

        Assert.True(result.IsCompleted);
        Assert.Equal([0L, 1L], published.Select(static item => item.Sequence.Value).ToArray());
        Assert.All(published, static item => Assert.True(item.IsSuccessful));
        Assert.True(published[0].RebalanceResult?.PublishedMigration);
        Assert.Equal(reference.CurrentTopology.Version, durable.CurrentTopology.Version);
        Assert.Equal(reference.CurrentTopology.Version, result.FinalTopologyVersion);
        Assert.Equal(
            referenceResults[0].TelemetrySummary.Counters.AcceptedMoveCount,
            published[0].RebalanceResult?.TelemetrySummary.Counters.AcceptedMoveCount);

        var secondProcessing = published[1].ProcessingResult;
        var secondTelemetry = Assert.IsType<RadarProcessingTelemetry>(secondProcessing?.Telemetry);
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), secondProcessing?.TopologyVersion);
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), secondTelemetry.TopologyVersion);
        Assert.Equal(referenceResults[1].ProcessingResult.TopologyVersion, secondProcessing?.TopologyVersion);
        Assert.Equal(2, result.QueueSummary.ReleasedEnvelopeCount);
        Assert.False(result.QueueSummary.HasBlockingEnvelope);
    }
}
