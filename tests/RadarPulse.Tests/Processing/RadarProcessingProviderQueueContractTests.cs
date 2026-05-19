using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingProviderQueueContractTests
{
    [Fact]
    public void ProviderQueueEnumValuesAreStable()
    {
        Assert.Equal(1, (int)RadarProcessingProviderQueueFullMode.ReturnFull);
        Assert.Equal(2, (int)RadarProcessingProviderQueueFullMode.Wait);

        Assert.Equal(1, (int)RadarProcessingProviderQueueShutdownMode.Drain);
        Assert.Equal(2, (int)RadarProcessingProviderQueueShutdownMode.CancelQueued);

        Assert.Equal(1, (int)RadarProcessingQueuedBatchEnqueueStatus.Accepted);
        Assert.Equal(2, (int)RadarProcessingQueuedBatchEnqueueStatus.Full);
        Assert.Equal(3, (int)RadarProcessingQueuedBatchEnqueueStatus.TimedOut);
        Assert.Equal(4, (int)RadarProcessingQueuedBatchEnqueueStatus.Canceled);
        Assert.Equal(5, (int)RadarProcessingQueuedBatchEnqueueStatus.Closed);
        Assert.Equal(6, (int)RadarProcessingQueuedBatchEnqueueStatus.Faulted);

        Assert.Equal(1, (int)RadarProcessingQueuedBatchProcessingStatus.Succeeded);
        Assert.Equal(2, (int)RadarProcessingQueuedBatchProcessingStatus.FailedProcessing);
        Assert.Equal(3, (int)RadarProcessingQueuedBatchProcessingStatus.FailedValidation);
        Assert.Equal(4, (int)RadarProcessingQueuedBatchProcessingStatus.FailedMigration);
        Assert.Equal(5, (int)RadarProcessingQueuedBatchProcessingStatus.Canceled);
        Assert.Equal(6, (int)RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault);

        Assert.Equal(0, (int)RadarProcessingQueuedSessionStatus.NotStarted);
        Assert.Equal(1, (int)RadarProcessingQueuedSessionStatus.Running);
        Assert.Equal(2, (int)RadarProcessingQueuedSessionStatus.Draining);
        Assert.Equal(3, (int)RadarProcessingQueuedSessionStatus.Completed);
        Assert.Equal(4, (int)RadarProcessingQueuedSessionStatus.Faulted);
        Assert.Equal(5, (int)RadarProcessingQueuedSessionStatus.Canceled);
        Assert.Equal(6, (int)RadarProcessingQueuedSessionStatus.Disposed);

        Assert.Equal(1, (int)RadarProcessingProviderQueueRecentDetailKind.Enqueue);
        Assert.Equal(2, (int)RadarProcessingProviderQueueRecentDetailKind.Dequeue);
        Assert.Equal(3, (int)RadarProcessingProviderQueueRecentDetailKind.Processing);
    }

    [Fact]
    public void ProviderQueueOptionsUseConservativeBoundedDefaults()
    {
        var options = RadarProcessingProviderQueueOptions.Default;

        Assert.Equal(1, options.Capacity);
        Assert.Equal(RadarProcessingProviderQueueFullMode.Wait, options.FullMode);
        Assert.Null(options.EnqueueTimeout);
        Assert.False(options.HasEnqueueTimeout);
        Assert.Equal(RadarProcessingProviderQueueShutdownMode.Drain, options.ShutdownMode);
        Assert.Equal(16, options.RecentDetailCapacity);
    }

    [Fact]
    public void ProviderQueueOptionsComposeExplicitSettings()
    {
        var timeout = TimeSpan.FromMilliseconds(250);

        var options = new RadarProcessingProviderQueueOptions(
            capacity: 4,
            fullMode: RadarProcessingProviderQueueFullMode.Wait,
            enqueueTimeout: timeout,
            shutdownMode: RadarProcessingProviderQueueShutdownMode.CancelQueued,
            recentDetailCapacity: 3);

        Assert.Equal(4, options.Capacity);
        Assert.Equal(RadarProcessingProviderQueueFullMode.Wait, options.FullMode);
        Assert.Equal(timeout, options.EnqueueTimeout);
        Assert.True(options.HasEnqueueTimeout);
        Assert.Equal(RadarProcessingProviderQueueShutdownMode.CancelQueued, options.ShutdownMode);
        Assert.Equal(3, options.RecentDetailCapacity);
    }

    [Fact]
    public void ProviderQueueOptionsRejectInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueOptions(capacity: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueOptions(fullMode: (RadarProcessingProviderQueueFullMode)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueOptions(shutdownMode: (RadarProcessingProviderQueueShutdownMode)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueOptions(recentDetailCapacity: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueOptions(enqueueTimeout: TimeSpan.Zero));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingProviderQueueOptions(
                fullMode: RadarProcessingProviderQueueFullMode.ReturnFull,
                enqueueTimeout: TimeSpan.FromMilliseconds(1)));
    }

    [Fact]
    public void QueuedBatchSequenceIsMonotonicAndRejectsNegativeValues()
    {
        var sequence = RadarProcessingQueuedBatchSequence.Initial;

        Assert.Equal(0, sequence.Value);
        Assert.Equal(1, sequence.Next().Value);
        Assert.Equal("0", sequence.ToString());
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarProcessingQueuedBatchSequence(-1));
    }

    [Fact]
    public void QueuedBatchRequiresOwnedPayloadAndCarriesSnapshotCost()
    {
        var ownedBatch = CreateOwnedBatch();
        var queued = new RadarProcessingQueuedBatch(
            new RadarProcessingQueuedBatchSequence(7),
            ownedBatch,
            ownedSnapshotTime: TimeSpan.FromMilliseconds(2),
            ownedSnapshotAllocatedBytes: 64);

        Assert.Equal(7, queued.Sequence.Value);
        Assert.Same(ownedBatch, queued.Batch);
        Assert.Equal(TimeSpan.FromMilliseconds(2), queued.OwnedSnapshotTime);
        Assert.Equal(64, queued.OwnedSnapshotAllocatedBytes);
        Assert.Equal(0, queued.EnqueuedTimestamp);
        Assert.Equal(1, queued.StreamEventCount);
        Assert.Equal(2, queued.PayloadBytes);
        Assert.Equal(2, queued.PayloadValueCount);
        Assert.Equal(15, queued.RawValueChecksum);

        var builder = CreateSingleEventBuilder();
        builder.ConsumeLeased(batch =>
        {
            Assert.Equal(RadarEventBatchLifetime.Leased, batch.Lifetime);
            Assert.Throws<ArgumentException>(() =>
                new RadarProcessingQueuedBatch(RadarProcessingQueuedBatchSequence.Initial, batch));
        });

        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingQueuedBatch(RadarProcessingQueuedBatchSequence.Initial, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQueuedBatch(
                RadarProcessingQueuedBatchSequence.Initial,
            ownedBatch,
            ownedSnapshotTime: TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQueuedBatch(
                RadarProcessingQueuedBatchSequence.Initial,
                ownedBatch,
                ownedSnapshotAllocatedBytes: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQueuedBatch(
                RadarProcessingQueuedBatchSequence.Initial,
                ownedBatch,
                enqueuedTimestamp: -1));
    }

    [Fact]
    public void EnqueueResultsSeparateAcceptedFromRejectedStatuses()
    {
        var queued = new RadarProcessingQueuedBatch(
            RadarProcessingQueuedBatchSequence.Initial,
            CreateOwnedBatch());

        var accepted = RadarProcessingQueuedBatchEnqueueResult.Accepted(
            queued,
            TimeSpan.FromMilliseconds(3));
        var full = RadarProcessingQueuedBatchEnqueueResult.Full(
            TimeSpan.FromMilliseconds(4),
            "queue full");
        var timedOut = RadarProcessingQueuedBatchEnqueueResult.TimedOut(
            TimeSpan.FromMilliseconds(5));
        var canceled = RadarProcessingQueuedBatchEnqueueResult.Canceled();
        var closed = RadarProcessingQueuedBatchEnqueueResult.Closed();
        var faulted = RadarProcessingQueuedBatchEnqueueResult.Faulted(message: "session faulted");

        Assert.True(accepted.IsAccepted);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Accepted, accepted.Status);
        Assert.Same(queued, accepted.Batch);
        Assert.Equal(RadarProcessingQueuedBatchSequence.Initial, accepted.Sequence);
        Assert.Equal(TimeSpan.FromMilliseconds(3), accepted.EnqueueWaitTime);

        Assert.False(full.IsAccepted);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Full, full.Status);
        Assert.Null(full.Batch);
        Assert.Null(full.Sequence);
        Assert.Equal(TimeSpan.FromMilliseconds(4), full.EnqueueWaitTime);
        Assert.Equal("queue full", full.Message);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.TimedOut, timedOut.Status);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Canceled, canceled.Status);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Closed, closed.Status);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Faulted, faulted.Status);
        Assert.Equal("session faulted", faulted.Message);

        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingQueuedBatchEnqueueResult.Accepted(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingQueuedBatchEnqueueResult.Full(TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingQueuedBatchEnqueueResult.Faulted(message: null!));
    }

    [Fact]
    public void ProcessingResultsSeparateSuccessFailureCancellationAndSkippedStatuses()
    {
        var sequence = new RadarProcessingQueuedBatchSequence(3);
        var processingResult = CreateProcessingResult(new RadarProcessingTopologyVersion(9));

        var succeeded = RadarProcessingQueuedBatchProcessingResult.Succeeded(sequence, processingResult);
        var failedProcessing = RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
            sequence.Next(),
            "processing failed",
            processingResult);
        var failedValidation = RadarProcessingQueuedBatchProcessingResult.FailedValidation(
            new RadarProcessingQueuedBatchSequence(5),
            "validation failed");
        var failedMigration = RadarProcessingQueuedBatchProcessingResult.FailedMigration(
            new RadarProcessingQueuedBatchSequence(6),
            "migration failed");
        var canceled = RadarProcessingQueuedBatchProcessingResult.Canceled(
            new RadarProcessingQueuedBatchSequence(7));
        var skipped = RadarProcessingQueuedBatchProcessingResult.SkippedAfterFault(
            new RadarProcessingQueuedBatchSequence(8),
            "already faulted");

        Assert.True(succeeded.IsSuccessful);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.Succeeded, succeeded.Status);
        Assert.Same(processingResult, succeeded.ProcessingResult);
        Assert.Equal(new RadarProcessingTopologyVersion(9), succeeded.TopologyVersion);

        Assert.False(failedProcessing.IsSuccessful);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.FailedProcessing, failedProcessing.Status);
        Assert.Same(processingResult, failedProcessing.ProcessingResult);
        Assert.Equal("processing failed", failedProcessing.Message);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.FailedValidation, failedValidation.Status);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.FailedMigration, failedMigration.Status);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.Canceled, canceled.Status);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault, skipped.Status);
        Assert.Equal("already faulted", skipped.Message);

        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingQueuedBatchProcessingResult.Succeeded(sequence, (RadarProcessingResult)null!));
        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingQueuedBatchProcessingResult.FailedProcessing(sequence, null!));
    }

    [Fact]
    public void ProviderQueueTelemetrySummaryCarriesCountersAndRejectsInvalidShapes()
    {
        var summary = new RadarProcessingProviderQueueTelemetrySummary(
            ownedSnapshotCount: 2,
            ownedSnapshotPayloadBytes: 128,
            ownedSnapshotAllocatedBytes: 256,
            totalOwnedSnapshotTime: TimeSpan.FromMilliseconds(3),
            enqueueAttemptCount: 3,
            enqueuedBatchCount: 2,
            enqueueFullCount: 1,
            enqueueTimedOutCount: 1,
            totalEnqueueWaitTime: TimeSpan.FromMilliseconds(5),
            dequeuedBatchCount: 2,
            completedBatchCount: 1,
            failedBatchCount: 1,
            totalDrainTime: TimeSpan.FromMilliseconds(7),
            queueDepthHighWatermark: 2,
            queuedPayloadBytesHighWatermark: 128,
            ownedSnapshotPayloadValueCount: 64,
            totalProviderToProcessingLatency: TimeSpan.FromMilliseconds(11),
            recentDetails:
            [
                new RadarProcessingProviderQueueRecentDetail(
                    RadarProcessingProviderQueueRecentDetailKind.Enqueue,
                    RadarProcessingQueuedBatchSequence.Initial,
                    enqueueStatus: RadarProcessingQueuedBatchEnqueueStatus.Accepted,
                    payloadBytes: 64,
                    payloadValueCount: 32)
            ],
            droppedRecentDetailCount: 3);

        Assert.Equal(2, summary.OwnedSnapshotCount);
        Assert.Equal(128, summary.OwnedSnapshotPayloadBytes);
        Assert.Equal(64, summary.OwnedSnapshotPayloadValueCount);
        Assert.Equal(256, summary.OwnedSnapshotAllocatedBytes);
        Assert.Equal(TimeSpan.FromMilliseconds(3), summary.TotalOwnedSnapshotTime);
        Assert.Equal(3, summary.EnqueueAttemptCount);
        Assert.Equal(2, summary.EnqueuedBatchCount);
        Assert.Equal(1, summary.EnqueueFullCount);
        Assert.Equal(1, summary.EnqueueTimedOutCount);
        Assert.Equal(TimeSpan.FromMilliseconds(5), summary.TotalEnqueueWaitTime);
        Assert.Equal(2, summary.DequeuedBatchCount);
        Assert.Equal(1, summary.CompletedBatchCount);
        Assert.Equal(1, summary.FailedBatchCount);
        Assert.Equal(TimeSpan.FromMilliseconds(7), summary.TotalDrainTime);
        Assert.Equal(2, summary.QueueDepthHighWatermark);
        Assert.Equal(128, summary.QueuedPayloadBytesHighWatermark);
        Assert.Equal(TimeSpan.FromMilliseconds(11), summary.TotalProviderToProcessingLatency);
        Assert.Single(summary.RecentDetails);
        Assert.Equal(1, summary.RetainedRecentDetailCount);
        Assert.Equal(3, summary.DroppedRecentDetailCount);
        Assert.Equal(4.0, summary.OwnedSnapshotAllocation.AllocatedBytesPerPayloadValue);
        Assert.True(summary.HasBackpressure);
        Assert.False(RadarProcessingProviderQueueTelemetrySummary.Empty.HasBackpressure);
        Assert.Same(RadarProcessingOwnedSnapshotAllocationSummary.Empty, RadarProcessingOwnedSnapshotAllocationSummary.Empty);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueTelemetrySummary(ownedSnapshotCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueTelemetrySummary(totalOwnedSnapshotTime: TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueTelemetrySummary(
                enqueueAttemptCount: 1,
                enqueuedBatchCount: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueTelemetrySummary(
                dequeuedBatchCount: 1,
                completedBatchCount: 1,
                failedBatchCount: 1));
        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingProviderQueueTelemetrySummary(
                recentDetails: new RadarProcessingProviderQueueRecentDetail[] { null! }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueTelemetrySummary(droppedRecentDetailCount: -1));
    }

    [Fact]
    public void OwnedSnapshotAllocationSummaryComputesRatiosAndRejectsInvalidValues()
    {
        var summary = new RadarProcessingOwnedSnapshotAllocationSummary(
            snapshotCount: 2,
            payloadBytes: 128,
            payloadValueCount: 64,
            allocatedBytes: 256,
            elapsed: TimeSpan.FromMilliseconds(3));

        Assert.Equal(128.0, summary.AllocatedBytesPerSnapshot);
        Assert.Equal(2.0, summary.AllocatedBytesPerPayloadByte);
        Assert.Equal(4.0, summary.AllocatedBytesPerPayloadValue);
        Assert.Equal(64.0, summary.PayloadBytesPerSnapshot);
        Assert.Equal(0.0, RadarProcessingOwnedSnapshotAllocationSummary.Empty.AllocatedBytesPerPayloadValue);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingOwnedSnapshotAllocationSummary(snapshotCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingOwnedSnapshotAllocationSummary(elapsed: TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void QueuedSessionResultCopiesResultCollectionsAndKeepsIntakeSeparateFromProcessing()
    {
        var queued = new RadarProcessingQueuedBatch(
            RadarProcessingQueuedBatchSequence.Initial,
            CreateOwnedBatch());
        var enqueueResults = new List<RadarProcessingQueuedBatchEnqueueResult>
        {
            RadarProcessingQueuedBatchEnqueueResult.Accepted(queued),
            RadarProcessingQueuedBatchEnqueueResult.Full()
        };
        var processingResults = new List<RadarProcessingQueuedBatchProcessingResult>
        {
            RadarProcessingQueuedBatchProcessingResult.Succeeded(
                RadarProcessingQueuedBatchSequence.Initial,
                CreateProcessingResult())
        };
        var telemetry = new RadarProcessingProviderQueueTelemetrySummary(
            enqueueAttemptCount: 2,
            enqueuedBatchCount: 1,
            enqueueFullCount: 1,
            dequeuedBatchCount: 1,
            completedBatchCount: 1);

        var result = new RadarProcessingQueuedSessionResult(
            RadarProcessingQueuedSessionStatus.Completed,
            telemetry,
            enqueueResults,
            processingResults,
            "complete");
        enqueueResults.Clear();
        processingResults.Clear();

        Assert.True(result.IsCompleted);
        Assert.False(result.IsFaulted);
        Assert.False(result.IsCanceled);
        Assert.Same(telemetry, result.Telemetry);
        Assert.Equal(2, result.EnqueueResults.Count);
        Assert.Single(result.ProcessingResults);
        Assert.Equal("complete", result.Message);
        Assert.True(result.EnqueueResults[0].IsAccepted);
        Assert.False(result.EnqueueResults[1].IsAccepted);
        Assert.True(result.ProcessingResults[0].IsSuccessful);

        var faulted = new RadarProcessingQueuedSessionResult(RadarProcessingQueuedSessionStatus.Faulted);
        var canceled = new RadarProcessingQueuedSessionResult(RadarProcessingQueuedSessionStatus.Canceled);

        Assert.True(faulted.IsFaulted);
        Assert.True(canceled.IsCanceled);
        Assert.Same(RadarProcessingProviderQueueTelemetrySummary.Empty, faulted.Telemetry);
        Assert.Empty(faulted.EnqueueResults);
        Assert.Empty(faulted.ProcessingResults);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQueuedSessionResult((RadarProcessingQueuedSessionStatus)255));
        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingQueuedSessionResult(
                RadarProcessingQueuedSessionStatus.Completed,
                enqueueResults: new RadarProcessingQueuedBatchEnqueueResult[] { null! }));
        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingQueuedSessionResult(
                RadarProcessingQueuedSessionStatus.Completed,
                processingResults: new RadarProcessingQueuedBatchProcessingResult[] { null! }));
    }

    private static RadarEventBatch CreateOwnedBatch() =>
        CreateSingleEventBuilder().Build();

    private static RadarEventBatchBuilder CreateSingleEventBuilder()
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
            gateCount: 2,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: [7, 8]);

        return builder;
    }

    private static RadarProcessingResult CreateProcessingResult(
        RadarProcessingTopologyVersion? topologyVersion = null)
    {
        var metrics = new RadarProcessingMetrics(
            ProcessedBatchCount: 1,
            ProcessedStreamEventCount: 1,
            ProcessedPayloadValueCount: 2,
            ActiveSourceCount: 1,
            RawValueChecksum: 15,
            ProcessingChecksum: 21);

        return new RadarProcessingResult(
            RadarProcessingExecutionMode.Sequential,
            partitionCount: 1,
            shardCount: 1,
            metrics,
            RadarProcessingValidationResult.Valid(metrics),
            topologyVersion: topologyVersion);
    }
}
