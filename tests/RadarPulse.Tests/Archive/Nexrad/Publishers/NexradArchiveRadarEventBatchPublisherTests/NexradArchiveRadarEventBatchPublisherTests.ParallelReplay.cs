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
    public void PublishFileParallelMatchesSequentialRadarEventBatchReplay()
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
        var sourceUniverse = ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse;
        var publisher = new NexradArchiveRadarEventBatchPublisher(new FakeArchiveBZip2Decompressor(
            new Dictionary<byte, byte[]>
            {
                [1] = firstRecordBytes,
                [2] = secondRecordBytes,
                [3] = thirdRecordBytes
            },
            new Dictionary<byte, int>
            {
                [1] = 50
            }));
        var sequentialCapture = new CapturingRadarEventBatchPublisher();
        var parallelCapture = new CapturingRadarEventBatchPublisher();

        try
        {
            var sequential = publisher.PublishFile(
                path,
                sequentialCapture,
                new ArchiveRadarEventBatchPublishOptions(sourceUniverse, degreeOfParallelism: 1),
                CancellationToken.None);
            var parallel = publisher.PublishFile(
                path,
                parallelCapture,
                new ArchiveRadarEventBatchPublishOptions(sourceUniverse, degreeOfParallelism: 2),
                CancellationToken.None);

            Assert.Equal(1, sequential.DegreeOfParallelism);
            Assert.Equal(2, parallel.DegreeOfParallelism);
            Assert.Equal(sequential.FilePath, parallel.FilePath);
            Assert.Equal(sequential.Decompressor, parallel.Decompressor);
            Assert.Equal(sequential.FileSizeBytes, parallel.FileSizeBytes);
            Assert.Equal(sequential.CompressedRecordCount, parallel.CompressedRecordCount);
            Assert.Equal(sequential.CompressedBytes, parallel.CompressedBytes);
            Assert.Equal(sequential.DecompressedBytes, parallel.DecompressedBytes);
            Assert.Equal(sequential.BatchCount, parallel.BatchCount);
            Assert.Equal(sequential.EventCount, parallel.EventCount);
            Assert.Equal(sequential.PayloadBytes, parallel.PayloadBytes);
            Assert.Equal(sequential.PayloadValueCount, parallel.PayloadValueCount);
            Assert.Equal(sequential.RawValueChecksum, parallel.RawValueChecksum);
            Assert.Equal(sequential.StreamSchemaVersion, parallel.StreamSchemaVersion);
            Assert.Equal(sequential.DictionaryVersion, parallel.DictionaryVersion);
            Assert.Equal(sequential.SourceUniverseVersion, parallel.SourceUniverseVersion);

            var sequentialBatch = Assert.Single(sequentialCapture.Batches);
            var parallelBatch = Assert.Single(parallelCapture.Batches);
            Assert.Equal(sequentialBatch.Events.ToArray(), parallelBatch.Events.ToArray());
            Assert.Equal(sequentialBatch.Payload.ToArray(), parallelBatch.Payload.ToArray());
            Assert.Equal(
                RadarEventBatchMetrics.Compute(sequentialBatch),
                RadarEventBatchMetrics.Compute(parallelBatch));
            Assert.Equal(
                RadarStreamDictionarySnapshotMetrics.Compute(sequential.DictionarySnapshot),
                RadarStreamDictionarySnapshotMetrics.Compute(parallel.DictionarySnapshot));

            var expectedMetrics = RadarEventBatchMetrics.Compute(sequentialBatch);
            var validation = RadarEventBatchValidator.Validate(
                parallelBatch,
                sourceUniverse,
                parallel.DictionarySnapshot,
                expectedMetrics);
            Assert.True(validation.IsValid, validation.Message);

            Assert.True(parallel.DictionarySnapshot.MomentCatalog.TryGetText(0, out var firstMoment));
            Assert.True(parallel.DictionarySnapshot.MomentCatalog.TryGetText(1, out var secondMoment));
            Assert.True(parallel.DictionarySnapshot.MomentCatalog.TryGetText(2, out var thirdMoment));
            Assert.Equal("REF", firstMoment);
            Assert.Equal("CFP", secondMoment);
            Assert.Equal("VEL", thirdMoment);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
