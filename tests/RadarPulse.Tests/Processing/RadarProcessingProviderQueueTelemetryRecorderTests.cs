using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingProviderQueueTelemetryRecorderTests
{
    [Fact]
    public void RecorderAccumulatesCountersAndRecentDetailsWithoutRetainingBatchPayload()
    {
        var recorder = new RadarProcessingProviderQueueTelemetryRecorder(
            new RadarProcessingProviderQueueOptions(capacity: 2, recentDetailCapacity: 4));
        var queued = new RadarProcessingQueuedBatch(
            new RadarProcessingQueuedBatchSequence(7),
            CreateOwnedBatch(1),
            ownedSnapshotTime: TimeSpan.FromMilliseconds(2),
            ownedSnapshotAllocatedBytes: 64);
        var processing = RadarProcessingQueuedBatchProcessingResult.Succeeded(
            queued.Sequence,
            CreateProcessingResult());

        recorder.RecordEnqueueResult(
            RadarProcessingQueuedBatchEnqueueResult.Accepted(
                queued,
                TimeSpan.FromMilliseconds(1)),
            queueDepth: 1,
            queuedPayloadBytes: queued.PayloadBytes);
        recorder.RecordDequeuedBatch(
            queued,
            providerToProcessingLatency: TimeSpan.FromMilliseconds(5),
            dequeueWaitTime: TimeSpan.FromMilliseconds(3));
        recorder.RecordProcessingResult(processing);
        recorder.AddDrainTime(TimeSpan.FromMilliseconds(7));

        var summary = recorder.CreateSummary();

        Assert.Equal(1, summary.OwnedSnapshotCount);
        Assert.Equal(1, summary.OwnedSnapshotEventCount);
        Assert.Equal(2, summary.OwnedSnapshotPayloadBytes);
        Assert.Equal(2, summary.OwnedSnapshotPayloadValueCount);
        Assert.Equal(64, summary.OwnedSnapshotAllocatedBytes);
        Assert.Equal(TimeSpan.FromMilliseconds(2), summary.TotalOwnedSnapshotTime);
        Assert.Equal(1, summary.EnqueueAttemptCount);
        Assert.Equal(1, summary.EnqueuedBatchCount);
        Assert.Equal(TimeSpan.FromMilliseconds(1), summary.TotalEnqueueWaitTime);
        Assert.Equal(TimeSpan.FromMilliseconds(3), summary.TotalDequeueWaitTime);
        Assert.Equal(1, summary.DequeuedBatchCount);
        Assert.Equal(1, summary.CompletedBatchCount);
        Assert.Equal(TimeSpan.FromMilliseconds(5), summary.TotalProviderToProcessingLatency);
        Assert.Equal(TimeSpan.FromMilliseconds(7), summary.TotalDrainTime);
        Assert.Equal(1, summary.QueueDepthHighWatermark);
        Assert.Equal(2, summary.QueuedPayloadBytesHighWatermark);
        Assert.Equal(3, summary.RecentDetails.Count);
        Assert.All(summary.RecentDetails, static detail =>
        {
            Assert.NotNull(detail);
            Assert.Equal(7, detail.Sequence?.Value);
        });
        Assert.Equal(
            [
                RadarProcessingProviderQueueRecentDetailKind.Enqueue,
                RadarProcessingProviderQueueRecentDetailKind.Dequeue,
                RadarProcessingProviderQueueRecentDetailKind.Processing
            ],
            summary.RecentDetails.Select(static detail => detail.Kind).ToArray());
    }

    [Fact]
    public void RecentDetailCapacityDropsOldestAndResetClearsCounters()
    {
        var recorder = new RadarProcessingProviderQueueTelemetryRecorder(
            new RadarProcessingProviderQueueOptions(capacity: 1, recentDetailCapacity: 2));

        recorder.RecordEnqueueResult(RadarProcessingQueuedBatchEnqueueResult.Full());
        recorder.RecordEnqueueResult(RadarProcessingQueuedBatchEnqueueResult.TimedOut(TimeSpan.FromMilliseconds(1)));
        recorder.RecordEnqueueResult(RadarProcessingQueuedBatchEnqueueResult.Canceled());

        var summary = recorder.CreateSummary();

        Assert.Equal(3, summary.EnqueueAttemptCount);
        Assert.Equal(1, summary.EnqueueFullCount);
        Assert.Equal(1, summary.EnqueueTimedOutCount);
        Assert.Equal(1, summary.EnqueueCanceledCount);
        Assert.Equal(2, summary.RecentDetails.Count);
        Assert.Equal(1, summary.DroppedRecentDetailCount);
        Assert.Equal(
            [
                RadarProcessingQueuedBatchEnqueueStatus.TimedOut,
                RadarProcessingQueuedBatchEnqueueStatus.Canceled
            ],
            summary.RecentDetails.Select(static detail => detail.EnqueueStatus).ToArray());

        recorder.Reset();
        var reset = recorder.CreateSummary();

        Assert.Equal(0, reset.EnqueueAttemptCount);
        Assert.Equal(TimeSpan.Zero, reset.TotalDequeueWaitTime);
        Assert.Empty(reset.RecentDetails);
        Assert.Equal(0, reset.DroppedRecentDetailCount);
    }

    [Fact]
    public void CountersOnlyRecentDetailCapacityDropsEveryDetail()
    {
        var recorder = new RadarProcessingProviderQueueTelemetryRecorder(
            new RadarProcessingProviderQueueOptions(capacity: 1, recentDetailCapacity: 0));

        recorder.RecordEnqueueResult(RadarProcessingQueuedBatchEnqueueResult.Full());
        recorder.RecordEnqueueResult(RadarProcessingQueuedBatchEnqueueResult.Closed());

        var summary = recorder.CreateSummary();

        Assert.Equal(2, summary.EnqueueAttemptCount);
        Assert.Empty(summary.RecentDetails);
        Assert.Equal(2, summary.DroppedRecentDetailCount);
    }

    private static RadarEventBatch CreateOwnedBatch(byte firstPayloadValue)
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
            payload: [firstPayloadValue, (byte)(firstPayloadValue + 1)]);
        return builder.Build();
    }

    private static RadarProcessingResult CreateProcessingResult()
    {
        var metrics = new RadarProcessingMetrics(
            ProcessedBatchCount: 1,
            ProcessedStreamEventCount: 1,
            ProcessedPayloadValueCount: 2,
            ActiveSourceCount: 1,
            RawValueChecksum: 3,
            ProcessingChecksum: 7);

        return new RadarProcessingResult(
            RadarProcessingExecutionMode.Sequential,
            partitionCount: 1,
            shardCount: 1,
            metrics,
            RadarProcessingValidationResult.Valid(metrics));
    }
}
