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
    public void PublishFilePublishesSequentialRadarEventBatch()
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
        var capture = new CapturingRadarEventBatchPublisher();
        var publisher = new NexradArchiveRadarEventBatchPublisher(new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
        {
            [1] = firstRecordBytes,
            [2] = secondRecordBytes
        }));

        try
        {
            var result = publisher.PublishFile(
                path,
                capture,
                ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar,
                CancellationToken.None);

            Assert.Equal(path, result.FilePath);
            Assert.Equal("fake", result.Decompressor);
            Assert.Equal(1, result.DegreeOfParallelism);
            Assert.Equal(2, result.CompressedRecordCount);
            Assert.Equal(compressedPayload1.Length + compressedPayload2.Length, result.CompressedBytes);
            Assert.Equal(firstRecordBytes.Length + secondRecordBytes.Length, result.DecompressedBytes);
            Assert.Equal(1, result.BatchCount);
            Assert.Equal(2, result.EventCount);
            Assert.Equal(8, result.PayloadBytes);
            Assert.Equal(8, result.PayloadValueCount);
            Assert.Equal(146, result.RawValueChecksum);
            Assert.Equal(StreamSchemaVersion.Current, result.StreamSchemaVersion);
            Assert.Equal(new DictionaryVersion(4), result.DictionaryVersion);
            Assert.Equal(SourceUniverseVersion.Initial, result.SourceUniverseVersion);

            Assert.Single(capture.Batches);
            var batch = capture.Batches[0];
            Assert.Equal(result.DictionaryVersion, batch.DictionaryVersion);
            Assert.Equal([0, 1, 66, 68, 0, 1, 2, 8], batch.Payload.ToArray());
            Assert.Collection(
                batch.Events.ToArray(),
                first =>
                {
                    Assert.Equal(0, first.SourceId);
                    Assert.Equal(0, first.RadarOrdinal);
                    Assert.Equal(0, first.MomentId);
                    Assert.Equal(0, first.ElevationSlot);
                    Assert.Equal(0, first.AzimuthBucket);
                    Assert.Equal(0, first.RangeBand);
                    Assert.Equal(1, first.SourceRecord);
                    Assert.Equal(1, first.SourceMessage);
                    Assert.Equal(1, first.RadialSequence);
                    Assert.Equal(0, first.GateStart);
                    Assert.Equal(4, first.GateCount);
                    Assert.Equal(RadarStreamWordSize.EightBit, first.WordSize);
                    Assert.Equal(2f, first.Scale);
                    Assert.Equal(66f, first.Offset);
                    Assert.Equal(0, first.PayloadOffset);
                    Assert.Equal(4, first.PayloadLength);
                },
                second =>
                {
                    Assert.Equal(1, second.SourceId);
                    Assert.Equal(0, second.RadarOrdinal);
                    Assert.Equal(1, second.MomentId);
                    Assert.Equal(0, second.ElevationSlot);
                    Assert.Equal(1, second.AzimuthBucket);
                    Assert.Equal(0, second.RangeBand);
                    Assert.Equal(2, second.SourceRecord);
                    Assert.Equal(1, second.SourceMessage);
                    Assert.Equal(2, second.RadialSequence);
                    Assert.Equal(4, second.PayloadOffset);
                    Assert.Equal(4, second.PayloadLength);
                });

            Assert.True(result.DictionarySnapshot.RadarCatalog.TryGetText(0, out var radarCode));
            Assert.True(result.DictionarySnapshot.MomentCatalog.TryGetText(0, out var firstMoment));
            Assert.True(result.DictionarySnapshot.MomentCatalog.TryGetText(1, out var secondMoment));
            Assert.Equal("KTLX", radarCode);
            Assert.Equal("REF", firstMoment);
            Assert.Equal("CFP", secondMoment);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
