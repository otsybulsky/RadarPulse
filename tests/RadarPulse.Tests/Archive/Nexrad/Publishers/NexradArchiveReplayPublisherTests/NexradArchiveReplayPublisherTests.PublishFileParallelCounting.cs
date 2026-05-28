using System.Buffers.Binary;
using System.Text;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class NexradArchiveReplayPublisherTests
{
    [Fact]
    public void PublishFileParallelCountingMatchesSequentialTotals()
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
        var replayPublisher = new NexradArchiveReplayPublisher(new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
        {
            [1] = firstRecordBytes,
            [2] = secondRecordBytes
        }));

        try
        {
            var sequential = replayPublisher.PublishFile(
                path,
                ArchiveReplayPublishOptions.Sequential,
                CancellationToken.None);
            var parallel = replayPublisher.PublishFile(
                path,
                new ArchiveReplayPublishOptions(2),
                CancellationToken.None);

            Assert.Equal(sequential.PublishedEvents, parallel.PublishedEvents);
            Assert.Equal(sequential.ValidEvents, parallel.ValidEvents);
            Assert.Equal(sequential.BelowThresholdEvents, parallel.BelowThresholdEvents);
            Assert.Equal(sequential.RangeFoldedEvents, parallel.RangeFoldedEvents);
            Assert.Equal(sequential.ClutterFilterNotAppliedEvents, parallel.ClutterFilterNotAppliedEvents);
            Assert.Equal(sequential.PointClutterFilterAppliedEvents, parallel.PointClutterFilterAppliedEvents);
            Assert.Equal(sequential.DualPolarizationFilteredEvents, parallel.DualPolarizationFilteredEvents);
            Assert.Equal(sequential.RawValueChecksum, parallel.RawValueChecksum);
            Assert.Equal(sequential.CalibratedValueScaledChecksum, parallel.CalibratedValueScaledChecksum);
            Assert.Equal(sequential.ChronologyChecksum, parallel.ChronologyChecksum);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

}
