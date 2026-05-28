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
    public void PublishFileSplitsMomentBlockAcrossRangeBands()
    {
        var recordBytes = BuildMessage(31, BuildEightBitType31Payload("REF", [10, 20, 30, 40], scale: 2f, offset: 66f));
        var compressedPayload = BuildFakeBZip2Payload(1);
        var path = WriteTempFile(
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload.Length, compressedPayload))
                .ToArray());
        var capture = new CapturingRadarEventBatchPublisher();
        var options = new ArchiveRadarEventBatchPublishOptions(new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 32,
            azimuthBucketCount: 720,
            rangeBandCount: 2));
        var publisher = new NexradArchiveRadarEventBatchPublisher(new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
        {
            [1] = recordBytes
        }));

        try
        {
            publisher.PublishFile(path, capture, options, CancellationToken.None);

            var batch = Assert.Single(capture.Batches);
            Assert.Equal([10, 20, 30, 40], batch.Payload.ToArray());
            Assert.Collection(
                batch.Events.ToArray(),
                first =>
                {
                    Assert.Equal(0, first.SourceId);
                    Assert.Equal(0, first.RangeBand);
                    Assert.Equal(0, first.GateStart);
                    Assert.Equal(2, first.GateCount);
                    Assert.Equal(0, first.PayloadOffset);
                    Assert.Equal(2, first.PayloadLength);
                },
                second =>
                {
                    Assert.Equal(1, second.SourceId);
                    Assert.Equal(1, second.RangeBand);
                    Assert.Equal(2, second.GateStart);
                    Assert.Equal(2, second.GateCount);
                    Assert.Equal(2, second.PayloadOffset);
                    Assert.Equal(2, second.PayloadLength);
                });
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
