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

    [Fact]
    public void PublishOptionsRejectInvalidParallelism()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ArchiveRadarEventBatchPublishOptions(
                ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse,
                degreeOfParallelism: 0));
    }

}
