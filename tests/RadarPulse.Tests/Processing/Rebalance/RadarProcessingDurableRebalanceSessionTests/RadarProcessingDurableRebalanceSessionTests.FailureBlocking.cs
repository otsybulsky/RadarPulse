using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingDurableRebalanceSessionTests
{
    [Fact]
    public async Task DurableRebalanceFailureBlocksLaterPublication()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var queue = new RadarProcessingDurableEnvelopeQueue();
        await using var session = new RadarProcessingDurableRebalanceSession(CreateSession(universe), queue);

        queue.Accept(BatchId("invalid"), CreateInvalidSourceBatch(universe.Version));
        queue.Accept(BatchId("valid"), CreateEmptyBatch(universe.Version));

        var invalid = queue.ClaimNext("worker-a").ClaimedEnvelope!;
        var valid = queue.ClaimNext("worker-b").ClaimedEnvelope!;

        await session.ProcessClaimedAsync(valid);
        Assert.Empty(session.CommitReady());

        await session.ProcessClaimedAsync(invalid);
        var published = session.CommitReady();
        var result = session.CreateResult();

        var failure = Assert.Single(published);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.FailedValidation, failure.Status);
        Assert.True(result.IsFaulted);
        Assert.Equal(1, result.QueueSummary.CompletedEnvelopeCount);
        Assert.Equal(1, result.QueueSummary.FailedEnvelopeCount);
        Assert.Equal(RadarProcessingDurableEnvelopeState.Failed, result.QueueSummary.FirstBlockingState);
    }
}
