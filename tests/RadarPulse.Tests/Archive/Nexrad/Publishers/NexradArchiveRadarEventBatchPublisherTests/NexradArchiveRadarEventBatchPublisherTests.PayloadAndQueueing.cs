using System.Buffers.Binary;
using System.Text;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Archive;

public sealed partial class NexradArchiveRadarEventBatchPublisherTests
{
    [Fact]
    public void PublishFilePreservesSixteenBitRawPayloadBytes()
    {
        var recordBytes = BuildMessage(31, BuildSixteenBitType31Payload("VEL", [129, 131], scale: 2f, offset: 129f));
        var compressedPayload = BuildFakeBZip2Payload(1);
        var path = WriteTempFile(
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload.Length, compressedPayload))
                .ToArray());
        var publisher = new NexradArchiveRadarEventBatchPublisher(new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
        {
            [1] = recordBytes
        }));

        try
        {
            var result = publisher.PublishFile(
                path,
                ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar,
                CancellationToken.None);

            Assert.Equal(1, result.BatchCount);
            Assert.Equal(1, result.EventCount);
            Assert.Equal(4, result.PayloadBytes);
            Assert.Equal(2, result.PayloadValueCount);
            Assert.Equal(260, result.RawValueChecksum);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public async Task PublishFileWithOwnedQueueingPublisherEnqueuesOwnedBatchAndPreservesChecksum()
    {
        var firstRecordBytes = BuildMessage(31, BuildEightBitType31Payload("REF", [0, 1, 66, 68], scale: 2f, offset: 66f));
        var secondRecordBytes = BuildMessage(31, BuildEightBitType31Payload("CFP", [0, 1, 2, 8], scale: 1f, offset: 8f));
        var compressedPayload1 = BuildFakeBZip2Payload(1);
        var compressedPayload2 = BuildFakeBZip2Payload(2);
        var path = WriteTempFile(
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload1.Length, compressedPayload1))
                .Concat(BuildCompressedRecord(compressedPayload2.Length, compressedPayload2))
                .ToArray());
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 1));
        using var queueingPublisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(queue);
        var publisher = new NexradArchiveRadarEventBatchPublisher(new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
        {
            [1] = firstRecordBytes,
            [2] = secondRecordBytes
        }));

        try
        {
            var result = publisher.PublishFile(
                path,
                queueingPublisher,
                ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar,
                CancellationToken.None);
            var providerResult = queueingPublisher.CreateResult();
            var dequeue = await queue.DequeueAsync();

            Assert.Equal(1, result.BatchCount);
            Assert.Equal(2, result.EventCount);
            Assert.Equal(8, result.PayloadBytes);
            Assert.Equal(8, result.PayloadValueCount);
            Assert.Equal(146, result.RawValueChecksum);
            Assert.Equal(1, providerResult.PublishAttemptCount);
            Assert.Equal(1, providerResult.AcceptedPublishCount);
            Assert.False(providerResult.HasRejectedPublish);
            Assert.Equal(1, providerResult.Telemetry.OwnedSnapshotCount);
            Assert.Equal(8, providerResult.Telemetry.OwnedSnapshotPayloadBytes);
            Assert.Equal(1, providerResult.Telemetry.EnqueuedBatchCount);
            Assert.Equal(RadarProcessingOwnedBatchDequeueStatus.Item, dequeue.Status);
            Assert.Equal(RadarEventBatchLifetime.Owned, dequeue.Batch!.Batch.Lifetime);
            Assert.Equal(result.DictionaryVersion, dequeue.Batch.Batch.DictionaryVersion);
            Assert.Equal([0, 1, 66, 68, 0, 1, 2, 8], dequeue.Batch.Batch.Payload.ToArray());
            Assert.True(dequeue.Batch.Batch.TryGetPayloadMetrics(out var payloadValueCount, out var rawValueChecksum));
            Assert.Equal(result.PayloadValueCount, payloadValueCount);
            Assert.Equal(result.RawValueChecksum, rawValueChecksum);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
