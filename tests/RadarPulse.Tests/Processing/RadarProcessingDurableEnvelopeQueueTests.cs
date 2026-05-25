using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingDurableEnvelopeQueueTests
{
    [Fact]
    public void DurableEnvelopeContractsRejectInvalidShapes()
    {
        Assert.Equal(1, (int)RadarProcessingDurableEnvelopeState.Pending);
        Assert.Equal(2, (int)RadarProcessingDurableEnvelopeState.Claimed);
        Assert.Equal(3, (int)RadarProcessingDurableEnvelopeState.Completed);
        Assert.Equal(4, (int)RadarProcessingDurableEnvelopeState.Committed);
        Assert.Equal(5, (int)RadarProcessingDurableEnvelopeState.Failed);
        Assert.Equal(6, (int)RadarProcessingDurableEnvelopeState.Poison);
        Assert.Equal(7, (int)RadarProcessingDurableEnvelopeState.Abandoned);
        Assert.Equal(8, (int)RadarProcessingDurableEnvelopeState.Canceled);
        Assert.Equal(9, (int)RadarProcessingDurableEnvelopeState.Released);

        Assert.Equal(1, (int)RadarProcessingDurableQueueOperationStatus.Accepted);
        Assert.Equal(14, (int)RadarProcessingDurableQueueOperationStatus.InvalidState);

        Assert.Throws<ArgumentException>(() => new RadarProcessingDurableBatchId(""));
        Assert.Throws<ArgumentException>(() => new RadarProcessingDurableBatchId("   "));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingDurableEnvelopeSnapshot(
                default,
                RadarProcessingQueuedBatchSequence.Initial,
                attempt: 0,
                RadarProcessingDurableEnvelopeState.Pending));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingDurableEnvelopeSnapshot(
                BatchId("batch"),
                RadarProcessingQueuedBatchSequence.Initial,
                attempt: -1,
                RadarProcessingDurableEnvelopeState.Pending));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingDurableEnvelopeSnapshot(
                BatchId("batch"),
                RadarProcessingQueuedBatchSequence.Initial,
                attempt: 0,
                (RadarProcessingDurableEnvelopeState)255));
        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingDurableQueueOperationResult(
                RadarProcessingDurableQueueOperationStatus.Accepted,
                message: null!));
        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingDurableQueueOperationResult(
                RadarProcessingDurableQueueOperationStatus.Claimed));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingDurableQueueOperationResult(
                (RadarProcessingDurableQueueOperationStatus)255));
    }

    [Fact]
    public void AcceptAssignsStableSequencesAndDuplicateIsIdempotent()
    {
        var queue = new RadarProcessingDurableEnvelopeQueue();

        var first = queue.Accept(BatchId("batch-a"), CreateOwnedBatch(1));
        var second = queue.Accept(BatchId("batch-b"), CreateOwnedBatch(3));
        var duplicate = queue.Accept(BatchId("batch-a"), CreateOwnedBatch(5));

        Assert.Equal(RadarProcessingDurableQueueOperationStatus.Accepted, first.Status);
        Assert.Equal(RadarProcessingDurableQueueOperationStatus.Accepted, second.Status);
        Assert.Equal(RadarProcessingDurableQueueOperationStatus.Duplicate, duplicate.Status);
        Assert.True(first.IsAccepted);
        Assert.True(duplicate.IsAccepted);
        Assert.Equal(0, first.Snapshot!.ProviderSequence.Value);
        Assert.Equal(1, second.Snapshot!.ProviderSequence.Value);
        Assert.Equal(0, duplicate.Snapshot!.ProviderSequence.Value);
        Assert.Equal(RadarProcessingDurableEnvelopeState.Pending, first.Snapshot.State);
        Assert.Equal(0, first.Snapshot.Attempt);
        Assert.Equal(2, queue.Count);
        Assert.Equal(2, queue.CreateSummary().PendingEnvelopeCount);
    }

    [Fact]
    public void ClaimCompleteCommitAndReleaseAdvanceState()
    {
        var queue = new RadarProcessingDurableEnvelopeQueue();
        var batchId = BatchId("batch-a");

        queue.Accept(batchId, CreateOwnedBatch(1));
        var claimed = queue.ClaimNext("worker-1");
        var completed = queue.Complete(batchId, "processed");
        var committed = queue.MarkCommitted(batchId);
        var released = queue.MarkReleased(batchId);
        var summary = queue.CreateSummary();

        Assert.Equal(RadarProcessingDurableQueueOperationStatus.Claimed, claimed.Status);
        Assert.NotNull(claimed.ClaimedEnvelope);
        Assert.Equal(batchId, claimed.ClaimedEnvelope!.BatchId);
        Assert.Equal(1, claimed.ClaimedEnvelope.Attempt);
        Assert.Equal("worker-1", claimed.Snapshot!.WorkerId);
        Assert.Equal(RadarProcessingDurableQueueOperationStatus.Completed, completed.Status);
        Assert.Equal(RadarProcessingDurableEnvelopeState.Completed, completed.Snapshot!.State);
        Assert.Equal("processed", completed.Snapshot.Message);
        Assert.Equal(RadarProcessingDurableQueueOperationStatus.Committed, committed.Status);
        Assert.Equal(RadarProcessingDurableQueueOperationStatus.Released, released.Status);
        Assert.Equal(RadarProcessingDurableEnvelopeState.Released, released.Snapshot!.State);
        Assert.Equal(1, summary.ReleasedEnvelopeCount);
        Assert.False(summary.HasUncommittedEnvelope);
        Assert.False(summary.HasBlockingEnvelope);
    }

    [Fact]
    public void InvalidTransitionsReturnInvalidStateOrNotFound()
    {
        var queue = new RadarProcessingDurableEnvelopeQueue();
        var batchId = BatchId("batch-a");

        queue.Accept(batchId, CreateOwnedBatch(1));

        var missing = queue.Complete(BatchId("missing"));
        var completeBeforeClaim = queue.Complete(batchId);
        var commitBeforeComplete = queue.MarkCommitted(batchId);
        var releaseBeforeTerminal = queue.MarkReleased(batchId);

        Assert.Equal(RadarProcessingDurableQueueOperationStatus.NotFound, missing.Status);
        Assert.True(missing.IsNotFound);
        Assert.Equal(RadarProcessingDurableQueueOperationStatus.InvalidState, completeBeforeClaim.Status);
        Assert.Equal(RadarProcessingDurableEnvelopeState.Pending, completeBeforeClaim.Snapshot!.State);
        Assert.True(completeBeforeClaim.IsInvalidState);
        Assert.Equal(RadarProcessingDurableQueueOperationStatus.InvalidState, commitBeforeComplete.Status);
        Assert.Equal(RadarProcessingDurableQueueOperationStatus.InvalidState, releaseBeforeTerminal.Status);
    }

    [Fact]
    public void FailAbandonAndRetryPreserveIdentityAndIncrementAttempt()
    {
        var queue = new RadarProcessingDurableEnvelopeQueue();
        var failedBatchId = BatchId("failed");
        var abandonedBatchId = BatchId("abandoned");

        queue.Accept(failedBatchId, CreateOwnedBatch(1));
        queue.Accept(abandonedBatchId, CreateOwnedBatch(3));

        Assert.Equal(failedBatchId, queue.ClaimNext("worker-a").ClaimedEnvelope!.BatchId);
        var failed = queue.Fail(failedBatchId, "transient failure");
        var retriedFailed = queue.Retry(failedBatchId, "retry transient failure");
        var reclamedFailed = queue.ClaimNext("worker-b");

        Assert.Equal(RadarProcessingDurableQueueOperationStatus.Failed, failed.Status);
        Assert.Equal(RadarProcessingDurableEnvelopeState.Failed, failed.Snapshot!.State);
        Assert.Equal(RadarProcessingDurableQueueOperationStatus.Retried, retriedFailed.Status);
        Assert.Equal(RadarProcessingDurableEnvelopeState.Pending, retriedFailed.Snapshot!.State);
        Assert.Equal(2, retriedFailed.Snapshot.Attempt);
        Assert.Equal(failedBatchId, reclamedFailed.ClaimedEnvelope!.BatchId);
        Assert.Equal(2, reclamedFailed.ClaimedEnvelope.Attempt);
        Assert.Equal(0, reclamedFailed.ClaimedEnvelope.ProviderSequence.Value);

        Assert.Equal(abandonedBatchId, queue.ClaimNext("worker-c").ClaimedEnvelope!.BatchId);
        var abandoned = queue.Abandon(abandonedBatchId, "worker disappeared");
        var retriedAbandoned = queue.Retry(abandonedBatchId);

        Assert.Equal(RadarProcessingDurableQueueOperationStatus.Abandoned, abandoned.Status);
        Assert.Equal(RadarProcessingDurableEnvelopeState.Abandoned, abandoned.Snapshot!.State);
        Assert.Equal(RadarProcessingDurableQueueOperationStatus.Retried, retriedAbandoned.Status);
        Assert.Equal(2, retriedAbandoned.Snapshot!.Attempt);
        Assert.Equal(2, queue.CreateSummary().RetryAttemptCount);
    }

    [Fact]
    public void PoisonAndSummaryExposeBlockingEnvelope()
    {
        var queue = new RadarProcessingDurableEnvelopeQueue();
        var releasedBatchId = BatchId("released");
        var poisonBatchId = BatchId("poison");
        var pendingBatchId = BatchId("pending");

        queue.Accept(releasedBatchId, CreateOwnedBatch(1));
        queue.Accept(poisonBatchId, CreateOwnedBatch(3));
        queue.Accept(pendingBatchId, CreateOwnedBatch(5));

        queue.ClaimNext("worker-a");
        queue.Complete(releasedBatchId);
        queue.MarkCommitted(releasedBatchId);
        queue.MarkReleased(releasedBatchId);

        queue.ClaimNext("worker-b");
        var poison = queue.Fail(poisonBatchId, "non-retryable", poison: true);
        var summary = queue.CreateSummary();

        Assert.Equal(RadarProcessingDurableQueueOperationStatus.Poisoned, poison.Status);
        Assert.Equal(RadarProcessingDurableEnvelopeState.Poison, poison.Snapshot!.State);
        Assert.Equal(3, summary.AcceptedEnvelopeCount);
        Assert.Equal(1, summary.ReleasedEnvelopeCount);
        Assert.Equal(1, summary.PoisonEnvelopeCount);
        Assert.Equal(1, summary.PendingEnvelopeCount);
        Assert.Equal(1, summary.OldestUncommittedSequence!.Value.Value);
        Assert.Equal(poisonBatchId, summary.FirstBlockingBatchId!.Value);
        Assert.Equal(1, summary.FirstBlockingSequence!.Value.Value);
        Assert.Equal(RadarProcessingDurableEnvelopeState.Poison, summary.FirstBlockingState);
        Assert.Equal("non-retryable", summary.FirstBlockingReason);
        Assert.True(summary.HasBlockingEnvelope);
    }

    [Fact]
    public void ClaimNextReturnsEmptyWhenNoPendingEnvelopeExists()
    {
        var queue = new RadarProcessingDurableEnvelopeQueue();

        var empty = queue.ClaimNext("worker");

        Assert.Equal(RadarProcessingDurableQueueOperationStatus.Empty, empty.Status);
        Assert.False(empty.HasEnvelope);
    }

    [Fact]
    public void QueueRejectsLeasedBatchWithoutAcceptingEnvelope()
    {
        var queue = new RadarProcessingDurableEnvelopeQueue();
        var builder = CreateBatchBuilder(1);

        builder.ConsumeLeased(batch =>
        {
            Assert.Equal(RadarEventBatchLifetime.Leased, batch.Lifetime);
            Assert.Throws<ArgumentException>(() => queue.Accept(BatchId("leased"), batch));
        });

        Assert.Equal(0, queue.Count);
        Assert.Equal(0, queue.CreateSummary().AcceptedEnvelopeCount);
    }

    private static RadarProcessingDurableBatchId BatchId(
        string value) =>
        new(value);

    private static RadarEventBatch CreateOwnedBatch(
        byte firstPayloadValue) =>
        CreateOwnedBatch([firstPayloadValue, (byte)(firstPayloadValue + 1)]);

    private static RadarEventBatch CreateOwnedBatch(
        byte[] payload) =>
        CreateBatchBuilder(payload).Build();

    private static RadarEventBatchBuilder CreateBatchBuilder(
        byte firstPayloadValue) =>
        CreateBatchBuilder([firstPayloadValue, (byte)(firstPayloadValue + 1)]);

    private static RadarEventBatchBuilder CreateBatchBuilder(
        byte[] payload)
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: 2);
        builder.AddEvent(
            new RadarStreamIdentity(
                sourceId: 0,
                radarOrdinal: 0,
                momentId: 0,
                elevationSlot: 0,
                azimuthBucket: 0,
                rangeBand: 0,
                dictionaryVersion: DictionaryVersion.Initial,
                sourceUniverseVersion: SourceUniverseVersion.Initial),
            volumeTimestampUtcTicks: 1,
            messageTimestampUtcTicks: 2,
            sourceRecord: 0,
            sourceMessage: 0,
            radialSequence: 0,
            gateStart: 0,
            gateCount: (ushort)payload.Length,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: payload);

        return builder;
    }
}
