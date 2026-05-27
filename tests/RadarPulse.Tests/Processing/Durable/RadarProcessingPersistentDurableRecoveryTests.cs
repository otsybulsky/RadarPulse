using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingPersistentDurableRecoveryTests
{
    [Fact]
    public void ClaimedEnvelopeBlocksAfterRestartUntilExplicitAbandon()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var queue = CreateQueue(path);
            queue.Accept(BatchId("first"), CreateOwnedBatch(1));
            queue.Accept(BatchId("second"), CreateOwnedBatch(3));
            var claimed = queue.ClaimNext("worker-a").ClaimedEnvelope!;

            var restored = CreateQueue(path);
            var blocked = restored.CreateSummary();
            var abandoned = restored.Abandon(claimed.BatchId, "stale worker lease");
            var afterAbandon = CreateQueue(path).CreateSummary();

            Assert.Equal(RadarProcessingDurableEnvelopeState.Claimed, blocked.FirstBlockingState);
            Assert.Equal(claimed.BatchId, blocked.FirstBlockingBatchId!.Value);
            Assert.Equal("claimed envelope has not completed", blocked.FirstBlockingReason);
            Assert.Equal(RadarProcessingDurableQueueOperationStatus.Abandoned, abandoned.Status);
            Assert.Equal(RadarProcessingDurableEnvelopeState.Abandoned, afterAbandon.FirstBlockingState);
            Assert.Equal("stale worker lease", afterAbandon.FirstBlockingReason);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AbandonedAttemptCanRetryAfterRestartAndPreservesAttempt()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var queue = CreateQueue(path);
            var batchId = BatchId("retryable");

            queue.Accept(batchId, CreateOwnedBatch(1));
            queue.ClaimNext("worker-a");
            queue.Abandon(batchId, "worker disappeared");

            var restored = CreateQueue(path);
            var retry = restored.Retry(batchId, "retry after restart");
            var claimed = CreateQueue(path).ClaimNext("worker-b").ClaimedEnvelope!;

            Assert.Equal(RadarProcessingDurableQueueOperationStatus.Retried, retry.Status);
            Assert.Equal(2, retry.Snapshot!.Attempt);
            Assert.Equal(RadarProcessingDurableEnvelopeState.Pending, retry.Snapshot.State);
            Assert.Equal(batchId, claimed.BatchId);
            Assert.Equal(2, claimed.Attempt);
            Assert.Equal(1, CreateQueue(path).CreateSummary().RetryAttemptCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void PoisonStatePersistsAsReadinessBlockerAfterRestart()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var queue = CreateQueue(path);
            var batchId = BatchId("poison");

            queue.Accept(batchId, CreateOwnedBatch(1));
            queue.ClaimNext("worker-a");
            queue.Fail(batchId, "non-retryable", poison: true);

            var summary = CreateQueue(path).CreateSummary();

            Assert.Equal(1, summary.PoisonEnvelopeCount);
            Assert.Equal(batchId, summary.FirstBlockingBatchId!.Value);
            Assert.Equal(RadarProcessingDurableEnvelopeState.Poison, summary.FirstBlockingState);
            Assert.Equal("non-retryable", summary.FirstBlockingReason);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CompletedUncommittedEnvelopeRemainsCommitInputAfterRestart()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var queue = CreateQueue(path);
            var batchId = BatchId("completed");

            queue.Accept(batchId, CreateOwnedBatch(1));
            queue.ClaimNext("worker-a");
            queue.Complete(batchId, "processed");

            var restored = CreateQueue(path);
            var summary = restored.CreateSummary();
            var committed = restored.MarkCommitted(batchId);
            var released = CreateQueue(path).MarkReleased(batchId);
            var finalSummary = CreateQueue(path).CreateSummary();

            Assert.Equal(1, summary.CompletedEnvelopeCount);
            Assert.Equal(RadarProcessingDurableEnvelopeState.Completed, summary.FirstBlockingState);
            Assert.Equal("completed envelope has not committed", summary.FirstBlockingReason);
            Assert.Equal(RadarProcessingDurableQueueOperationStatus.Committed, committed.Status);
            Assert.Equal(RadarProcessingDurableQueueOperationStatus.Released, released.Status);
            Assert.False(finalSummary.HasBlockingEnvelope);
            Assert.Equal(1, finalSummary.ReleasedEnvelopeCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CanceledEnvelopeCleanupReleasesAfterRestart()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var queue = CreateQueue(path);

            queue.Accept(BatchId("first"), CreateOwnedBatch(1));
            queue.Accept(BatchId("second"), CreateOwnedBatch(3));
            queue.ClaimNext("worker-a");
            queue.CancelOpen("operator canceled");

            var canceledSummary = CreateQueue(path).CreateSummary();
            var releasedCount = CreateQueue(path).ReleaseCanceled("cleanup after restart");
            var releasedSummary = CreateQueue(path).CreateSummary();

            Assert.Equal(2, canceledSummary.CanceledEnvelopeCount);
            Assert.Equal(RadarProcessingDurableEnvelopeState.Canceled, canceledSummary.FirstBlockingState);
            Assert.Equal("operator canceled", canceledSummary.FirstBlockingReason);
            Assert.Equal(2, releasedCount);
            Assert.Equal(2, releasedSummary.ReleasedEnvelopeCount);
            Assert.False(releasedSummary.HasBlockingEnvelope);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static RadarProcessingDurableEnvelopeQueue CreateQueue(
        string path) =>
        new(new RadarProcessingFileDurableEnvelopeStore(path));

    private static RadarProcessingDurableBatchId BatchId(
        string value) =>
        new(value);

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "radarpulse-m026-recovery-",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static RadarEventBatch CreateOwnedBatch(
        byte firstPayloadValue) =>
        new(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            SourceUniverseVersion.Initial,
            new[]
            {
                new RadarStreamEvent(
                    sourceId: 0,
                    radarOrdinal: 0,
                    volumeTimestampUtcTicks: 90,
                    messageTimestampUtcTicks: 100,
                    sourceRecord: 1,
                    sourceMessage: 1,
                    radialSequence: 1,
                    elevationSlot: 0,
                    azimuthBucket: 0,
                    rangeBand: 0,
                    momentId: 0,
                    gateStart: 0,
                    gateCount: 1,
                    wordSize: RadarStreamWordSize.EightBit,
                    scale: 1.0f,
                    offset: 0.0f,
                    statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                    payloadOffset: 0,
                    payloadLength: 1),
                new RadarStreamEvent(
                    sourceId: 1,
                    radarOrdinal: 0,
                    volumeTimestampUtcTicks: 90,
                    messageTimestampUtcTicks: 200,
                    sourceRecord: 1,
                    sourceMessage: 1,
                    radialSequence: 2,
                    elevationSlot: 0,
                    azimuthBucket: 1,
                    rangeBand: 0,
                    momentId: 0,
                    gateStart: 0,
                    gateCount: 1,
                    wordSize: RadarStreamWordSize.EightBit,
                    scale: 1.0f,
                    offset: 0.0f,
                    statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                    payloadOffset: 1,
                    payloadLength: 1)
            },
            new[] { firstPayloadValue, (byte)(firstPayloadValue + 1) });
}
