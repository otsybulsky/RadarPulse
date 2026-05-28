using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingDurableRebalanceSessionTests
{
    [Fact]
    public async Task DurableRebalancePreservesAcceptedMoveAgainstDirectReference()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var reference = CreateSession(universe);
        var durable = CreateSession(universe);
        var batch = CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1]);
        var referenceResult = reference.Process(batch);
        var queue = new RadarProcessingDurableEnvelopeQueue();
        await using var session = new RadarProcessingDurableRebalanceSession(durable, queue);

        queue.Accept(BatchId("batch-0"), batch);

        var result = await session.DrainAsync();

        Assert.True(result.IsCompleted);
        Assert.Equal(reference.CurrentTopology.Version, durable.CurrentTopology.Version);
        Assert.Equal(reference.CurrentTopology.Version, result.FinalTopologyVersion);
        var processing = Assert.Single(result.ProcessingResults);
        Assert.True(processing.IsSuccessful);
        Assert.True(processing.RebalanceResult?.Validation.IsValid);
        Assert.Equal(referenceResult.PublishedMigration, processing.RebalanceResult?.PublishedMigration);
        Assert.Equal(
            referenceResult.TelemetrySummary.Counters.AcceptedMoveCount,
            processing.RebalanceResult?.TelemetrySummary.Counters.AcceptedMoveCount);
        Assert.Equal(1, result.QueueSummary.ReleasedEnvelopeCount);
        Assert.False(result.QueueSummary.HasBlockingEnvelope);
    }
}
