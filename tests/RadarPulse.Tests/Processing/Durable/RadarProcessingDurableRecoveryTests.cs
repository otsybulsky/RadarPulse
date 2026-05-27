using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingDurableRecoveryTests
{
    [Fact]
    public async Task AbandonedAttemptCanRetryAndCommitWhenPolicyPermits()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var queue = new RadarProcessingDurableEnvelopeQueue();
        await using var session = new RadarProcessingDurableProcessingSession(CreateCore(universe), queue);
        var batchId = BatchId("retryable");

        queue.Accept(batchId, CreateBatch(universe.Version, [0], messageTimestampBase: 100));
        var firstClaim = queue.ClaimNext("worker-a").ClaimedEnvelope!;
        var abandoned = queue.Abandon(firstClaim.BatchId, "worker disappeared");
        var retry = session.RetryOrPoison(
            batchId,
            new RadarProcessingDurableRetryPolicy(maxAttempts: 2),
            retryMessage: "retry abandoned attempt");
        var secondClaim = queue.ClaimNext("worker-b").ClaimedEnvelope!;

        await session.ProcessClaimedAsync(secondClaim);
        var published = session.CommitReady();
        var result = session.CreateResult();

        Assert.Equal(RadarProcessingDurableQueueOperationStatus.Abandoned, abandoned.Status);
        Assert.Equal(RadarProcessingDurableQueueOperationStatus.Retried, retry.Status);
        Assert.Equal(2, retry.Snapshot!.Attempt);
        Assert.Equal(batchId, secondClaim.BatchId);
        Assert.Equal(2, secondClaim.Attempt);
        Assert.Equal([0L], published.Select(static item => item.Sequence.Value).ToArray());
        Assert.True(result.IsCompleted);
        Assert.Equal(1, result.QueueSummary.RetryAttemptCount);
        Assert.Equal(1, result.QueueSummary.ReleasedEnvelopeCount);
        Assert.False(result.QueueSummary.HasBlockingEnvelope);
    }

    [Fact]
    public void ExhaustedRetryPolicyMarksEnvelopePoison()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var queue = new RadarProcessingDurableEnvelopeQueue();
        using var session = new RadarProcessingDurableProcessingSession(CreateCore(universe), queue);
        var batchId = BatchId("poison");

        queue.Accept(batchId, CreateBatch(universe.Version, [0], messageTimestampBase: 100));
        var claim = queue.ClaimNext("worker-a").ClaimedEnvelope!;
        queue.Fail(claim.BatchId, "non-retryable failure");

        var poison = session.RetryOrPoison(
            batchId,
            RadarProcessingDurableRetryPolicy.NoRetry,
            poisonMessage: "retry budget exhausted");
        var summary = queue.CreateSummary();

        Assert.Equal(RadarProcessingDurableQueueOperationStatus.Poisoned, poison.Status);
        Assert.Equal(RadarProcessingDurableEnvelopeState.Poison, poison.Snapshot!.State);
        Assert.Equal(1, summary.PoisonEnvelopeCount);
        Assert.Equal(batchId, summary.FirstBlockingBatchId!.Value);
        Assert.Equal(RadarProcessingDurableEnvelopeState.Poison, summary.FirstBlockingState);
        Assert.Equal("retry budget exhausted", summary.FirstBlockingReason);
    }

    [Fact]
    public async Task RetrySuccessCanUnblockLaterCompletedEnvelope()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var queue = new RadarProcessingDurableEnvelopeQueue();
        await using var session = new RadarProcessingDurableProcessingSession(CreateCore(universe), queue);
        var firstBatchId = BatchId("first");
        var secondBatchId = BatchId("second");

        queue.Accept(firstBatchId, CreateBatch(universe.Version, [0], messageTimestampBase: 100));
        queue.Accept(secondBatchId, CreateBatch(universe.Version, [1], messageTimestampBase: 200));

        var firstClaim = queue.ClaimNext("worker-a").ClaimedEnvelope!;
        var secondClaim = queue.ClaimNext("worker-b").ClaimedEnvelope!;

        queue.Abandon(firstClaim.BatchId, "worker disappeared");
        await session.ProcessClaimedAsync(secondClaim);
        Assert.Empty(session.CommitReady());

        session.RetryOrPoison(
            firstBatchId,
            new RadarProcessingDurableRetryPolicy(maxAttempts: 2));
        var retriedFirst = queue.ClaimNext("worker-c").ClaimedEnvelope!;
        await session.ProcessClaimedAsync(retriedFirst);
        var published = session.CommitReady();
        var result = session.CreateResult();

        Assert.Equal([0L, 1L], published.Select(static item => item.Sequence.Value).ToArray());
        Assert.True(result.IsCompleted);
        Assert.Equal(2, result.QueueSummary.ReleasedEnvelopeCount);
        Assert.Equal(1, result.QueueSummary.RetryAttemptCount);
        Assert.False(result.QueueSummary.HasBlockingEnvelope);
    }

    [Fact]
    public async Task CancellationCleanupReleasesOpenEnvelopesAndPendingCompletions()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var queue = new RadarProcessingDurableEnvelopeQueue();
        await using var session = new RadarProcessingDurableProcessingSession(CreateCore(universe), queue);

        queue.Accept(BatchId("first"), CreateBatch(universe.Version, [0], messageTimestampBase: 100));
        queue.Accept(BatchId("second"), CreateBatch(universe.Version, [1], messageTimestampBase: 200));

        var firstClaim = queue.ClaimNext("worker-a").ClaimedEnvelope!;
        queue.ClaimNext("worker-b");
        await session.ProcessClaimedAsync(firstClaim);

        var canceled = session.CancelAndCleanup("operator canceled durable processing");

        Assert.True(canceled.IsCanceled);
        Assert.Equal(2, canceled.QueueSummary.ReleasedEnvelopeCount);
        Assert.False(canceled.QueueSummary.HasUncommittedEnvelope);
        Assert.False(canceled.QueueSummary.HasBlockingEnvelope);
        Assert.Empty(session.CommitReady());
    }

    private static RadarProcessingDurableBatchId BatchId(
        string value) =>
        new(value);

    private static RadarProcessingCore CreateCore(
        RadarSourceUniverse universe) =>
        new(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount: universe.SourceCount,
                shardCount: Math.Min(2, universe.SourceCount)));

    private static RadarSourceUniverse CreateUniverse(
        int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarEventBatch CreateBatch(
        SourceUniverseVersion sourceUniverseVersion,
        int[] sourceIds,
        long messageTimestampBase)
    {
        var events = new RadarStreamEvent[sourceIds.Length];
        var payload = new byte[sourceIds.Length];

        for (var i = 0; i < sourceIds.Length; i++)
        {
            events[i] = new RadarStreamEvent(
                sourceIds[i],
                radarOrdinal: 0,
                volumeTimestampUtcTicks: 90,
                messageTimestampUtcTicks: messageTimestampBase + i,
                sourceRecord: 1,
                sourceMessage: 1,
                radialSequence: i,
                elevationSlot: 0,
                azimuthBucket: (ushort)sourceIds[i],
                rangeBand: 0,
                momentId: 0,
                gateStart: 0,
                gateCount: 1,
                wordSize: RadarStreamWordSize.EightBit,
                scale: 1.0f,
                offset: 0.0f,
                statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                payloadOffset: i,
                payloadLength: 1);
            payload[i] = (byte)(i + 1);
        }

        return new RadarEventBatch(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            events,
            payload);
    }
}
