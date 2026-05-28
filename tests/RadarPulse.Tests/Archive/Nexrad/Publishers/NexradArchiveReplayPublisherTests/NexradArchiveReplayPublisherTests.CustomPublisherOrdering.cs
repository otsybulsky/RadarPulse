using System.Buffers.Binary;
using System.Text;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class NexradArchiveReplayPublisherTests
{
    [Fact]
    public void PublishFileParallelDrainsCustomPublisherInSourceOrder()
    {
        var firstRecordBytes = BuildMessage(31, BuildEightBitType31Payload("REF", [66, 68], scale: 2f, offset: 66f));
        var secondRecordBytes = BuildMessage(31, BuildEightBitType31Payload("VEL", [129, 131], scale: 2f, offset: 129f));
        var compressedPayload1 = BuildFakeBZip2Payload(1);
        var compressedPayload2 = BuildFakeBZip2Payload(2);
        var path = WriteTempFile(
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload1.Length, compressedPayload1))
                .Concat(BuildCompressedRecord(compressedPayload2.Length, compressedPayload2))
                .ToArray());
        var capture = new CapturingReplayPublisher();
        var replayPublisher = new NexradArchiveReplayPublisher(new FakeArchiveBZip2Decompressor(
            new Dictionary<byte, byte[]>
            {
                [1] = firstRecordBytes,
                [2] = secondRecordBytes
            },
            new Dictionary<byte, int>
            {
                [1] = 40,
                [2] = 0
            }));

        try
        {
            var result = replayPublisher.PublishFile(
                path,
                capture,
                new ArchiveReplayPublishOptions(2),
                CancellationToken.None);

            Assert.Equal(4, result.PublishedEvents);
            Assert.Collection(
                capture.Events,
                first =>
                {
                    Assert.Equal(new ArchiveTwoRadialSourceOrder(1, 1, 1), first.SourceOrder);
                    Assert.Equal("REF", first.MomentName);
                    Assert.Equal(0, first.GateIndex);
                },
                second =>
                {
                    Assert.Equal(new ArchiveTwoRadialSourceOrder(1, 1, 1), second.SourceOrder);
                    Assert.Equal("REF", second.MomentName);
                    Assert.Equal(1, second.GateIndex);
                },
                third =>
                {
                    Assert.Equal(new ArchiveTwoRadialSourceOrder(2, 1, 2), third.SourceOrder);
                    Assert.Equal("VEL", third.MomentName);
                    Assert.Equal(0, third.GateIndex);
                },
                fourth =>
                {
                    Assert.Equal(new ArchiveTwoRadialSourceOrder(2, 1, 2), fourth.SourceOrder);
                    Assert.Equal("VEL", fourth.MomentName);
                    Assert.Equal(1, fourth.GateIndex);
                });
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

}
