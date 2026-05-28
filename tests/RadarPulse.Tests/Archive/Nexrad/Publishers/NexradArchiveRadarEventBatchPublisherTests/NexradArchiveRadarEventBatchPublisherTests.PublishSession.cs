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
    public void PublishSessionMatchesPublisherTotalsAcrossRepeatedParallelRuns()
    {
        var firstRecordBytes = BuildMessage(31, BuildEightBitType31Payload("REF", [1, 2, 3], scale: 2f, offset: 66f));
        var secondRecordBytes = BuildMessage(31, BuildEightBitType31Payload("CFP", [0, 1, 8], scale: 1f, offset: 8f));
        var thirdRecordBytes = BuildMessage(31, BuildSixteenBitType31Payload("VEL", [129, 131], scale: 2f, offset: 129f));
        var compressedPayload1 = BuildFakeBZip2Payload(1);
        var compressedPayload2 = BuildFakeBZip2Payload(2);
        var compressedPayload3 = BuildFakeBZip2Payload(3);
        var path = WriteTempFile(
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload1.Length, compressedPayload1))
                .Concat(BuildCompressedRecord(compressedPayload2.Length, compressedPayload2))
                .Concat(BuildCompressedRecord(compressedPayload3.Length, compressedPayload3))
                .ToArray());
        var decompressor = new FakeArchiveBZip2Decompressor(
            new Dictionary<byte, byte[]>
            {
                [1] = firstRecordBytes,
                [2] = secondRecordBytes,
                [3] = thirdRecordBytes
            },
            new Dictionary<byte, int>
            {
                [1] = 50
            });
        var options = new ArchiveRadarEventBatchPublishOptions(
            ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse,
            degreeOfParallelism: 2);

        try
        {
            var expected = new NexradArchiveRadarEventBatchPublisher(decompressor)
                .PublishFile(path, options, CancellationToken.None);
            using var session = new NexradArchiveRadarEventBatchPublishSession(decompressor, options);
            var first = session.PublishFile(path, CancellationToken.None);
            var second = session.PublishFile(path, CancellationToken.None);
            var leasedCapture = new LeasedCapturingRadarEventBatchPublisher();
            var captured = session.PublishFile(path, leasedCapture, CancellationToken.None);

            AssertArchiveRadarEventBatchPublishTotalsEqual(expected, first);
            AssertArchiveRadarEventBatchPublishTotalsEqual(expected, second);
            AssertArchiveRadarEventBatchPublishTotalsEqual(expected, captured);
            Assert.Equal(
                RadarStreamDictionarySnapshotMetrics.Compute(expected.DictionarySnapshot),
                RadarStreamDictionarySnapshotMetrics.Compute(first.DictionarySnapshot));
            Assert.Equal(
                RadarStreamDictionarySnapshotMetrics.Compute(expected.DictionarySnapshot),
                RadarStreamDictionarySnapshotMetrics.Compute(second.DictionarySnapshot));
            Assert.Equal(
                RadarStreamDictionarySnapshotMetrics.Compute(expected.DictionarySnapshot),
                RadarStreamDictionarySnapshotMetrics.Compute(captured.DictionarySnapshot));
            Assert.Single(leasedCapture.Batches);
            Assert.Equal(RadarEventBatchLifetime.Owned, leasedCapture.Batches[0].Lifetime);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
