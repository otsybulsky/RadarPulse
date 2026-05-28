using System.Buffers.Binary;
using System.Text;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class NexradArchiveReplayPublisherTests
{
    [Fact]
    public void PublishFilePublishesGateMomentEventsInSourceOrderAndReportsTotals()
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
        var capture = new CapturingReplayPublisher();
        var countingPublisher = new ArchiveReplayCountingPublisher(capture);
        var replayPublisher = new NexradArchiveReplayPublisher(new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
        {
            [1] = firstRecordBytes,
            [2] = secondRecordBytes
        }));

        try
        {
            var result = replayPublisher.PublishFile(
                path,
                countingPublisher,
                ArchiveReplayPublishOptions.Sequential,
                CancellationToken.None);

            Assert.Equal(path, result.FilePath);
            Assert.Equal("fake", result.Decompressor);
            Assert.Equal(1, result.DegreeOfParallelism);
            Assert.Equal(2, result.CompressedRecordCount);
            Assert.Equal(compressedPayload1.Length + compressedPayload2.Length, result.CompressedBytes);
            Assert.Equal(firstRecordBytes.Length + secondRecordBytes.Length, result.DecompressedBytes);
            Assert.Equal(8, result.PublishedEvents);
            Assert.Equal(3, result.ValidEvents);
            Assert.Equal(1, result.BelowThresholdEvents);
            Assert.Equal(1, result.RangeFoldedEvents);
            Assert.Equal(1, result.ClutterFilterNotAppliedEvents);
            Assert.Equal(1, result.PointClutterFilterAppliedEvents);
            Assert.Equal(1, result.DualPolarizationFilteredEvents);
            Assert.Equal(0, result.ReservedEvents);
            Assert.Equal(0, result.UnsupportedEvents);
            Assert.Equal(146, result.RawValueChecksum);
            Assert.Equal(1_000, result.CalibratedValueScaledChecksum);
            Assert.Equal(countingPublisher.PublishedEvents, result.PublishedEvents);
            Assert.Equal(countingPublisher.ChronologyChecksum, result.ChronologyChecksum);

            Assert.Collection(
                capture.Events,
                first =>
                {
                    Assert.Equal(new ArchiveTwoRadialSourceOrder(1, 1, 1), first.SourceOrder);
                    Assert.Equal(0, first.GateIndex);
                    Assert.Equal(ArchiveTwoGateMomentStatus.BelowThreshold, first.Status);
                },
                second =>
                {
                    Assert.Equal(new ArchiveTwoRadialSourceOrder(1, 1, 1), second.SourceOrder);
                    Assert.Equal(1, second.GateIndex);
                    Assert.Equal(ArchiveTwoGateMomentStatus.RangeFolded, second.Status);
                },
                third =>
                {
                    Assert.Equal(new ArchiveTwoRadialSourceOrder(1, 1, 1), third.SourceOrder);
                    Assert.Equal(2, third.GateIndex);
                    Assert.Equal(ArchiveTwoGateMomentStatus.Valid, third.Status);
                    Assert.Equal(0, third.CalibratedValue);
                },
                fourth =>
                {
                    Assert.Equal(new ArchiveTwoRadialSourceOrder(1, 1, 1), fourth.SourceOrder);
                    Assert.Equal(3, fourth.GateIndex);
                    Assert.Equal(ArchiveTwoGateMomentStatus.Valid, fourth.Status);
                    Assert.Equal(1, fourth.CalibratedValue);
                },
                fifth =>
                {
                    Assert.Equal(new ArchiveTwoRadialSourceOrder(2, 1, 2), fifth.SourceOrder);
                    Assert.Equal(0, fifth.GateIndex);
                    Assert.Equal(ArchiveTwoGateMomentStatus.ClutterFilterNotApplied, fifth.Status);
                },
                sixth =>
                {
                    Assert.Equal(new ArchiveTwoRadialSourceOrder(2, 1, 2), sixth.SourceOrder);
                    Assert.Equal(1, sixth.GateIndex);
                    Assert.Equal(ArchiveTwoGateMomentStatus.PointClutterFilterApplied, sixth.Status);
                },
                seventh =>
                {
                    Assert.Equal(new ArchiveTwoRadialSourceOrder(2, 1, 2), seventh.SourceOrder);
                    Assert.Equal(2, seventh.GateIndex);
                    Assert.Equal(ArchiveTwoGateMomentStatus.DualPolarizationFiltered, seventh.Status);
                },
                eighth =>
                {
                    Assert.Equal(new ArchiveTwoRadialSourceOrder(2, 1, 2), eighth.SourceOrder);
                    Assert.Equal(3, eighth.GateIndex);
                    Assert.Equal(ArchiveTwoGateMomentStatus.Valid, eighth.Status);
                    Assert.Equal(0, eighth.CalibratedValue);
                });
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

}
