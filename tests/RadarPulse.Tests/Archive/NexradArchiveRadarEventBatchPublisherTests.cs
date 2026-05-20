using System.Buffers.Binary;
using System.Text;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Archive;

public sealed class NexradArchiveRadarEventBatchPublisherTests
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

    [Fact]
    public void StreamBenchmarkMeasuresConsistentIterations()
    {
        var firstRecordBytes = BuildMessage(31, BuildEightBitType31Payload("REF", [1, 2, 3], scale: 2f, offset: 66f));
        var secondRecordBytes = BuildMessage(31, BuildSixteenBitType31Payload("VEL", [129, 131], scale: 2f, offset: 129f));
        var compressedPayload1 = BuildFakeBZip2Payload(1);
        var compressedPayload2 = BuildFakeBZip2Payload(2);
        var path = WriteTempFile(
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload1.Length, compressedPayload1))
                .Concat(BuildCompressedRecord(compressedPayload2.Length, compressedPayload2))
                .ToArray());
        var benchmark = new NexradArchiveRadarEventBatchStreamBenchmark(new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
        {
            [1] = firstRecordBytes,
            [2] = secondRecordBytes
        }));

        try
        {
            var result = benchmark.Measure(
                path,
                iterations: 2,
                warmupIterations: 1,
                degreeOfParallelism: 2,
                CancellationToken.None);

            Assert.Equal(path, result.FilePath);
            Assert.Equal("fake", result.Decompressor);
            Assert.Equal(2, result.Iterations);
            Assert.Equal(1, result.WarmupIterations);
            Assert.Equal(2, result.DegreeOfParallelism);
            Assert.Equal(StreamSchemaVersion.Current, result.StreamSchemaVersion);
            Assert.Equal(new DictionaryVersion(4), result.DictionaryVersion);
            Assert.Equal(SourceUniverseVersion.Initial, result.SourceUniverseVersion);
            Assert.Equal(2, result.CompressedRecordsPerIteration);
            Assert.Equal(compressedPayload1.Length + compressedPayload2.Length, result.CompressedBytesPerIteration);
            Assert.Equal(firstRecordBytes.Length + secondRecordBytes.Length, result.DecompressedBytesPerIteration);
            Assert.Equal(1, result.BatchesPerIteration);
            Assert.Equal(2, result.EventsPerIteration);
            Assert.Equal(7, result.PayloadBytesPerIteration);
            Assert.Equal(5, result.PayloadValuesPerIteration);
            Assert.Equal(266, result.RawValueChecksumPerIteration);
            Assert.Equal(1, result.RadarDictionaryEntries);
            Assert.Equal(2, result.MomentDictionaryEntries);
            Assert.NotEqual(0UL, result.DictionaryMappingChecksum);
            Assert.Equal(4, result.TotalCompressedRecords);
            Assert.Equal(4, result.TotalEvents);
            Assert.Equal(10, result.TotalPayloadValues);
            Assert.True(result.Elapsed > TimeSpan.Zero);
            Assert.True(result.AllocatedBytes > 0);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void StreamCacheBenchmarkMeasuresConsistentIterations()
    {
        var firstFileFirstRecord = BuildMessage(31, BuildEightBitType31Payload("REF", [1, 2, 3], scale: 2f, offset: 66f));
        var firstFileSecondRecord = BuildMessage(31, BuildSixteenBitType31Payload("VEL", [129, 131], scale: 2f, offset: 129f));
        var secondFileRecord = BuildMessage(31, BuildEightBitType31Payload("CFP", [4, 5], scale: 1f, offset: 8f));
        var compressedPayload1 = BuildFakeBZip2Payload(1);
        var compressedPayload2 = BuildFakeBZip2Payload(2);
        var compressedPayload3 = BuildFakeBZip2Payload(3);
        var directory = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        WriteTempFileInDirectory(
            directory,
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload1.Length, compressedPayload1))
                .Concat(BuildCompressedRecord(compressedPayload2.Length, compressedPayload2))
                .ToArray());
        WriteTempFileInDirectory(
            directory,
            "KTLX20260504_000846_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload3.Length, compressedPayload3))
                .ToArray());
        WriteTempFileInDirectory(directory, "notes.txt", [1, 2, 3]);
        var benchmark = new NexradArchiveRadarEventBatchStreamBenchmark(new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
        {
            [1] = firstFileFirstRecord,
            [2] = firstFileSecondRecord,
            [3] = secondFileRecord
        }));

        try
        {
            var result = benchmark.MeasureCache(
                directory,
                date: null,
                radarId: null,
                maxFiles: 10,
                iterations: 2,
                warmupIterations: 1,
                degreeOfParallelism: 2,
                CancellationToken.None);

            Assert.Equal(directory, result.CachePath);
            Assert.Equal("fake", result.Decompressor);
            Assert.Equal(2, result.Iterations);
            Assert.Equal(1, result.WarmupIterations);
            Assert.Equal(2, result.DegreeOfParallelism);
            Assert.Equal(StreamSchemaVersion.Current, result.StreamSchemaVersion);
            Assert.Equal(SourceUniverseVersion.Initial, result.SourceUniverseVersion);
            Assert.Equal(3, result.ExaminedFilesPerIteration);
            Assert.Equal(1, result.SkippedFilesPerIteration);
            Assert.Equal(2, result.PublishedFilesPerIteration);
            Assert.Equal(3, result.CompressedRecordsPerIteration);
            Assert.Equal(
                compressedPayload1.Length + compressedPayload2.Length + compressedPayload3.Length,
                result.CompressedBytesPerIteration);
            Assert.Equal(
                firstFileFirstRecord.Length + firstFileSecondRecord.Length + secondFileRecord.Length,
                result.DecompressedBytesPerIteration);
            Assert.Equal(2, result.BatchesPerIteration);
            Assert.Equal(3, result.EventsPerIteration);
            Assert.Equal(9, result.PayloadBytesPerIteration);
            Assert.Equal(7, result.PayloadValuesPerIteration);
            Assert.Equal(275, result.RawValueChecksumPerIteration);
            Assert.Equal(6, result.TotalCompressedRecords);
            Assert.Equal(6, result.TotalEvents);
            Assert.Equal(14, result.TotalPayloadValues);
            Assert.True(result.Elapsed > TimeSpan.Zero);
            Assert.True(result.AllocatedBytes > 0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance)]
    [InlineData(RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly)]
    [InlineData(RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession)]
    public void RebalanceArchiveBenchmarkFileSupportsAsyncExecution(
        RadarProcessingSyntheticRebalanceBenchmarkMode mode)
    {
        var record = BuildMessage(31, BuildEightBitType31Payload("REF", [1, 2, 3], scale: 2f, offset: 66f));
        var compressedPayload = BuildFakeBZip2Payload(1);
        var path = WriteTempFile(
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload.Length, compressedPayload))
                .ToArray());
        var benchmark = new RadarProcessingArchiveRebalanceBenchmark(
            new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
            {
                [1] = record
            }));

        try
        {
            var result = benchmark.MeasureFile(
                path,
                mode,
                iterations: 1,
                warmupIterations: 1,
                partitionCount: 4,
                shardCount: 2,
                degreeOfParallelism: 1,
                CancellationToken.None,
                executionMode: RadarProcessingExecutionMode.AsyncShardTransport,
                asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 1));

            Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, result.ExecutionMode);
            Assert.True(result.HasWorkerTelemetry);
            Assert.NotNull(result.WorkerTelemetry);
            Assert.Equal(2, result.WorkerTelemetry.WorkerCount);
            Assert.Equal(1, result.WorkerTelemetry.QueueCapacity);
            Assert.Equal(result.BatchesPerIteration * result.Iterations, result.WorkerTelemetry.Counters.CompletedBatchCount);
            Assert.Equal(0, result.WorkerTelemetry.Counters.FailedBatchCount);
            Assert.Equal("fake", result.Decompressor);
            Assert.Equal(mode, result.Mode);
            Assert.Equal(1, result.BatchesPerIteration);
            Assert.Equal(1, result.EventsPerIteration);
            Assert.Equal(3, result.PayloadValuesPerIteration);
            Assert.True(result.ValidationSucceeded);
            Assert.NotEqual(0UL, result.ValidationChecksum);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void RebalanceArchiveBenchmarkFileSupportsQueuedOwnedProviderMode()
    {
        var record = BuildMessage(31, BuildEightBitType31Payload("REF", [1, 2, 3], scale: 2f, offset: 66f));
        var compressedPayload = BuildFakeBZip2Payload(1);
        var path = WriteTempFile(
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload.Length, compressedPayload))
                .ToArray());
        var benchmark = new RadarProcessingArchiveRebalanceBenchmark(
            new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
            {
                [1] = record
            }));

        try
        {
            var blocking = benchmark.MeasureFile(
                path,
                RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly,
                iterations: 1,
                warmupIterations: 0,
                partitionCount: 4,
                shardCount: 2,
                degreeOfParallelism: 1,
                CancellationToken.None);
            var queued = benchmark.MeasureFile(
                path,
                RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly,
                iterations: 1,
                warmupIterations: 0,
                partitionCount: 4,
                shardCount: 2,
                degreeOfParallelism: 1,
                CancellationToken.None,
                providerMode: RadarProcessingArchiveProviderMode.QueuedOwned,
                queueCapacity: 1);

            Assert.Equal(RadarProcessingArchiveProviderMode.BlockingBorrowed, blocking.ProviderMode);
            Assert.False(blocking.HasQueueTelemetry);
            Assert.Equal(0, blocking.QueueCapacity);
            Assert.Equal(RadarProcessingArchiveProviderMode.QueuedOwned, queued.ProviderMode);
            Assert.True(queued.HasQueueTelemetry);
            Assert.True(queued.HasRetentionTelemetry);
            Assert.Equal(1, queued.QueueCapacity);
            Assert.Equal(RadarProcessingQueuedProviderOverlapMode.None, queued.ProviderOverlapMode);
            Assert.Equal(RadarProcessingRetainedPayloadStrategy.SnapshotCopy, queued.RetentionStrategy);
            Assert.Equal(blocking.BatchesPerIteration, queued.BatchesPerIteration);
            Assert.Equal(blocking.EventsPerIteration, queued.EventsPerIteration);
            Assert.Equal(blocking.PayloadValuesPerIteration, queued.PayloadValuesPerIteration);
            Assert.Equal(blocking.ValidationChecksum, queued.ValidationChecksum);
            Assert.Equal(1, queued.QueueTelemetry.OwnedSnapshotCount);
            Assert.Equal(1, queued.QueueTelemetry.EnqueueAttemptCount);
            Assert.Equal(1, queued.QueueTelemetry.EnqueuedBatchCount);
            Assert.Equal(1, queued.QueueTelemetry.DequeuedBatchCount);
            Assert.Equal(1, queued.QueueTelemetry.CompletedBatchCount);
            Assert.Equal(0, queued.QueueTelemetry.FailedBatchCount);
            Assert.Equal(queued.PayloadBytesPerIteration, queued.QueueTelemetry.OwnedSnapshotPayloadBytes);
            Assert.Equal(queued.PayloadValuesPerIteration, queued.QueueTelemetry.OwnedSnapshotPayloadValueCount);
            Assert.Equal(1, queued.RetentionTelemetry.RetentionAttemptCount);
            Assert.Equal(1, queued.RetentionTelemetry.RetainedBatchCount);
            Assert.Equal(1, queued.RetentionTelemetry.ReleaseAttemptCount);
            Assert.Equal(1, queued.RetentionTelemetry.ReleaseNotRequiredCount);
            Assert.Equal(queued.QueueTelemetry.OwnedSnapshotPayloadBytes, queued.RetentionTelemetry.RetainedPayloadBytes);
            Assert.Equal(queued.QueueTelemetry.OwnedSnapshotPayloadValueCount, queued.RetentionTelemetry.RetainedPayloadValueCount);
            Assert.True(queued.OwnedSnapshotAllocatedBytes > 0);
            Assert.Equal(queued.QueueTelemetry.OwnedSnapshotAllocatedBytes, queued.OwnedSnapshotAllocatedBytes);
            Assert.True(queued.OwnedSnapshotElapsed >= TimeSpan.Zero);
            Assert.True(queued.QueueDrainElapsed >= TimeSpan.Zero);
            Assert.True(queued.AllocationSummary.OwnedSnapshotAllocatedBytesPerPayloadValue(queued.TotalPayloadValues) >= 0);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void RebalanceArchiveBenchmarkFileSupportsQueuedOwnedOverlapAndRetentionStrategy()
    {
        var record = BuildMessage(31, BuildEightBitType31Payload("REF", [1, 2, 3], scale: 2f, offset: 66f));
        var compressedPayload = BuildFakeBZip2Payload(1);
        var path = WriteTempFile(
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload.Length, compressedPayload))
                .ToArray());
        var benchmark = new RadarProcessingArchiveRebalanceBenchmark(
            new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
            {
                [1] = record
            }));

        try
        {
            var result = benchmark.MeasureFile(
                path,
                RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly,
                iterations: 1,
                warmupIterations: 0,
                partitionCount: 4,
                shardCount: 2,
                degreeOfParallelism: 1,
                CancellationToken.None,
                providerMode: RadarProcessingArchiveProviderMode.QueuedOwned,
                queueCapacity: 2,
                providerOverlapMode: RadarProcessingQueuedProviderOverlapMode.ProducerConsumer,
                retentionStrategy: RadarProcessingRetainedPayloadStrategy.PooledCopy,
                queueRetainedPayloadBytes: 4096);

            Assert.Equal(RadarProcessingArchiveProviderMode.QueuedOwned, result.ProviderMode);
            Assert.Equal(RadarProcessingQueuedProviderOverlapMode.ProducerConsumer, result.ProviderOverlapMode);
            Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.RetentionStrategy);
            Assert.Equal(4096, result.QueueRetainedPayloadBytes);
            Assert.True(result.HasQueueTelemetry);
            Assert.True(result.HasRetentionTelemetry);
            Assert.True(result.HasOverlapTelemetry);
            Assert.Equal(1, result.QueueTelemetry.EnqueuedBatchCount);
            Assert.Equal(1, result.QueueTelemetry.DequeuedBatchCount);
            Assert.Equal(1, result.QueueTelemetry.CompletedBatchCount);
            Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.RetentionTelemetry.Strategy);
            Assert.Equal(1, result.RetentionTelemetry.RetentionAttemptCount);
            Assert.Equal(1, result.RetentionTelemetry.RetainedBatchCount);
            Assert.Equal(1, result.RetentionTelemetry.ReleaseAttemptCount);
            Assert.Equal(1, result.RetentionTelemetry.ReleasedBatchCount);
            Assert.Equal(result.PayloadBytesPerIteration, result.RetentionTelemetry.RetainedPayloadBytes);
            Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.OverlapTelemetry.RetentionStrategy);
            Assert.Equal(result.RetentionTelemetry, result.OverlapTelemetry.RetentionTelemetry);
            Assert.Equal(result.QueueTelemetry.EnqueuedBatchCount, result.OverlapTelemetry.QueueTelemetry.EnqueuedBatchCount);
            Assert.Equal(result.QueueTelemetry.DequeuedBatchCount, result.OverlapTelemetry.QueueTelemetry.DequeuedBatchCount);
            Assert.Equal(result.QueueTelemetry.CompletedBatchCount, result.OverlapTelemetry.QueueTelemetry.CompletedBatchCount);
            Assert.True(result.OverlapTelemetry.Elapsed >= TimeSpan.Zero);
            Assert.True(result.ValidationSucceeded);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void RebalanceArchiveBenchmarkQueuedOwnedAsyncKeepsWorkerTelemetry()
    {
        var record = BuildMessage(31, BuildEightBitType31Payload("REF", [1, 2, 3], scale: 2f, offset: 66f));
        var compressedPayload = BuildFakeBZip2Payload(1);
        var path = WriteTempFile(
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload.Length, compressedPayload))
                .ToArray());
        var benchmark = new RadarProcessingArchiveRebalanceBenchmark(
            new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
            {
                [1] = record
            }));

        try
        {
            var result = benchmark.MeasureFile(
                path,
                RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
                iterations: 1,
                warmupIterations: 0,
                partitionCount: 4,
                shardCount: 2,
                degreeOfParallelism: 1,
                CancellationToken.None,
                executionMode: RadarProcessingExecutionMode.AsyncShardTransport,
                asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 1),
                providerMode: RadarProcessingArchiveProviderMode.QueuedOwned,
                queueCapacity: 1);

            Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, result.ExecutionMode);
            Assert.Equal(RadarProcessingArchiveProviderMode.QueuedOwned, result.ProviderMode);
            Assert.True(result.HasWorkerTelemetry);
            Assert.NotNull(result.WorkerTelemetry);
            Assert.Equal(1, result.WorkerTelemetry.Counters.CompletedBatchCount);
            Assert.Equal(0, result.WorkerTelemetry.Counters.FailedBatchCount);
            Assert.Equal(1, result.QueueTelemetry.CompletedBatchCount);
            Assert.Equal(1, result.QueueTelemetry.DequeuedBatchCount);
            Assert.True(result.ValidationSucceeded);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void RebalanceArchiveBenchmarkValidatesQueuedProviderOptions()
    {
        var record = BuildMessage(31, BuildEightBitType31Payload("REF", [1, 2, 3], scale: 2f, offset: 66f));
        var compressedPayload = BuildFakeBZip2Payload(1);
        var path = WriteTempFile(
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload.Length, compressedPayload))
                .ToArray());
        var benchmark = new RadarProcessingArchiveRebalanceBenchmark(
            new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
            {
                [1] = record
            }));

        try
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                benchmark.MeasureFile(
                    path,
                    RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
                    iterations: 1,
                    warmupIterations: 0,
                    partitionCount: 4,
                    shardCount: 2,
                    degreeOfParallelism: 1,
                    CancellationToken.None,
                    providerMode: (RadarProcessingArchiveProviderMode)255));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                benchmark.MeasureFile(
                    path,
                    RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
                    iterations: 1,
                    warmupIterations: 0,
                    partitionCount: 4,
                    shardCount: 2,
                    degreeOfParallelism: 1,
                    CancellationToken.None,
                    providerMode: RadarProcessingArchiveProviderMode.QueuedOwned,
                    queueCapacity: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                benchmark.MeasureFile(
                    path,
                    RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
                    iterations: 1,
                    warmupIterations: 0,
                    partitionCount: 4,
                    shardCount: 2,
                    degreeOfParallelism: 1,
                    CancellationToken.None,
                    providerMode: RadarProcessingArchiveProviderMode.QueuedOwned,
                    providerOverlapMode: (RadarProcessingQueuedProviderOverlapMode)255));
            Assert.Throws<InvalidOperationException>(() =>
                benchmark.MeasureFile(
                    path,
                    RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
                    iterations: 1,
                    warmupIterations: 0,
                    partitionCount: 4,
                    shardCount: 2,
                    degreeOfParallelism: 1,
                    CancellationToken.None,
                    providerOverlapMode: RadarProcessingQueuedProviderOverlapMode.ProducerConsumer));
            Assert.Throws<InvalidOperationException>(() =>
                benchmark.MeasureFile(
                    path,
                    RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
                    iterations: 1,
                    warmupIterations: 0,
                    partitionCount: 4,
                    shardCount: 2,
                    degreeOfParallelism: 1,
                    CancellationToken.None,
                    retentionStrategy: RadarProcessingRetainedPayloadStrategy.PooledCopy));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                benchmark.MeasureFile(
                    path,
                    RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
                    iterations: 1,
                    warmupIterations: 0,
                    partitionCount: 4,
                    shardCount: 2,
                    degreeOfParallelism: 1,
                    CancellationToken.None,
                    providerMode: RadarProcessingArchiveProviderMode.QueuedOwned,
                    queueRetainedPayloadBytes: 0));
            Assert.Throws<NotSupportedException>(() =>
                benchmark.MeasureFile(
                    path,
                    RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
                    iterations: 1,
                    warmupIterations: 0,
                    partitionCount: 4,
                    shardCount: 2,
                    degreeOfParallelism: 1,
                    CancellationToken.None,
                    providerMode: RadarProcessingArchiveProviderMode.QueuedOwned,
                    retentionStrategy: RadarProcessingRetainedPayloadStrategy.BuilderTransfer));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                benchmark.MeasureFile(
                    path,
                    RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
                    iterations: 1,
                    warmupIterations: 0,
                    partitionCount: 4,
                    shardCount: 2,
                    degreeOfParallelism: 1,
                    CancellationToken.None,
                    providerMode: RadarProcessingArchiveProviderMode.QueuedOwned,
                    providerOverlapMode: RadarProcessingQueuedProviderOverlapMode.ProducerConsumer,
                    overlapConsumerDelay: TimeSpan.FromMilliseconds(-1)));
            Assert.Throws<InvalidOperationException>(() =>
                benchmark.MeasureFile(
                    path,
                    RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
                    iterations: 1,
                    warmupIterations: 0,
                    partitionCount: 4,
                    shardCount: 2,
                    degreeOfParallelism: 1,
                    CancellationToken.None,
                    providerMode: RadarProcessingArchiveProviderMode.QueuedOwned,
                    overlapConsumerDelay: TimeSpan.FromMilliseconds(1)));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void RebalanceArchiveBenchmarkCacheAsyncMatchesSynchronousTotals()
    {
        var firstRecord = BuildMessage(31, BuildEightBitType31Payload("REF", [1, 2, 3], scale: 2f, offset: 66f));
        var secondRecord = BuildMessage(31, BuildEightBitType31Payload("VEL", [4, 5], scale: 1f, offset: 8f));
        var compressedPayload1 = BuildFakeBZip2Payload(1);
        var compressedPayload2 = BuildFakeBZip2Payload(2);
        var directory = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        WriteTempFileInDirectory(
            directory,
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload1.Length, compressedPayload1))
                .ToArray());
        WriteTempFileInDirectory(
            directory,
            "KTLX20260504_000846_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload2.Length, compressedPayload2))
                .ToArray());
        WriteTempFileInDirectory(directory, "notes.txt", [1, 2, 3]);
        var benchmark = new RadarProcessingArchiveRebalanceBenchmark(
            new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
            {
                [1] = firstRecord,
                [2] = secondRecord
            }));

        try
        {
            var synchronous = benchmark.MeasureCache(
                directory,
                date: null,
                radarId: null,
                maxFiles: 10,
                mode: RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly,
                iterations: 1,
                warmupIterations: 0,
                partitionCount: 4,
                shardCount: 2,
                degreeOfParallelism: 2,
                CancellationToken.None);
            var asynchronous = benchmark.MeasureCache(
                directory,
                date: null,
                radarId: null,
                maxFiles: 10,
                mode: RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly,
                iterations: 1,
                warmupIterations: 0,
                partitionCount: 4,
                shardCount: 2,
                degreeOfParallelism: 2,
                CancellationToken.None,
                executionMode: RadarProcessingExecutionMode.AsyncShardTransport,
                asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 1));

            Assert.Equal(RadarProcessingExecutionMode.PartitionedBarrier, synchronous.ExecutionMode);
            Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, asynchronous.ExecutionMode);
            Assert.NotNull(asynchronous.WorkerTelemetry);
            Assert.Equal(synchronous.ExaminedFilesPerIteration, asynchronous.ExaminedFilesPerIteration);
            Assert.Equal(synchronous.SkippedFilesPerIteration, asynchronous.SkippedFilesPerIteration);
            Assert.Equal(synchronous.PublishedFilesPerIteration, asynchronous.PublishedFilesPerIteration);
            Assert.Equal(synchronous.BatchesPerIteration, asynchronous.BatchesPerIteration);
            Assert.Equal(synchronous.EventsPerIteration, asynchronous.EventsPerIteration);
            Assert.Equal(synchronous.PayloadValuesPerIteration, asynchronous.PayloadValuesPerIteration);
            Assert.Equal(synchronous.RebalanceEvaluationCount, asynchronous.RebalanceEvaluationCount);
            Assert.Equal(synchronous.ValidationChecksum, asynchronous.ValidationChecksum);
            Assert.Equal(asynchronous.BatchesPerIteration, asynchronous.WorkerTelemetry.Counters.CompletedBatchCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RebalanceArchiveBenchmarkCacheQueuedOwnedAggregatesQueueTelemetry()
    {
        var firstRecord = BuildMessage(31, BuildEightBitType31Payload("REF", [1, 2, 3], scale: 2f, offset: 66f));
        var secondRecord = BuildMessage(31, BuildEightBitType31Payload("VEL", [4, 5], scale: 1f, offset: 8f));
        var compressedPayload1 = BuildFakeBZip2Payload(1);
        var compressedPayload2 = BuildFakeBZip2Payload(2);
        var directory = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        WriteTempFileInDirectory(
            directory,
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload1.Length, compressedPayload1))
                .ToArray());
        WriteTempFileInDirectory(
            directory,
            "KTLX20260504_000846_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload2.Length, compressedPayload2))
                .ToArray());
        WriteTempFileInDirectory(directory, "notes.txt", [1, 2, 3]);
        var benchmark = new RadarProcessingArchiveRebalanceBenchmark(
            new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
            {
                [1] = firstRecord,
                [2] = secondRecord
            }));

        try
        {
            var result = benchmark.MeasureCache(
                directory,
                date: null,
                radarId: null,
                maxFiles: 10,
                mode: RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
                iterations: 1,
                warmupIterations: 0,
                partitionCount: 4,
                shardCount: 2,
                degreeOfParallelism: 2,
                CancellationToken.None,
                providerMode: RadarProcessingArchiveProviderMode.QueuedOwned,
                queueCapacity: 1);

            Assert.Equal(RadarProcessingArchiveProviderMode.QueuedOwned, result.ProviderMode);
            Assert.True(result.HasQueueTelemetry);
            Assert.Equal(1, result.QueueCapacity);
            Assert.Equal(2, result.PublishedFilesPerIteration);
            Assert.Equal(2, result.BatchesPerIteration);
            Assert.Equal(2, result.QueueTelemetry.OwnedSnapshotCount);
            Assert.Equal(2, result.QueueTelemetry.EnqueueAttemptCount);
            Assert.Equal(2, result.QueueTelemetry.EnqueuedBatchCount);
            Assert.Equal(2, result.QueueTelemetry.DequeuedBatchCount);
            Assert.Equal(2, result.QueueTelemetry.CompletedBatchCount);
            Assert.Equal(result.PayloadValuesPerIteration, result.QueueTelemetry.OwnedSnapshotPayloadValueCount);
            Assert.Equal(2, result.RetentionTelemetry.RetentionAttemptCount);
            Assert.Equal(2, result.RetentionTelemetry.RetainedBatchCount);
            Assert.Equal(2, result.RetentionTelemetry.ReleaseAttemptCount);
            Assert.Equal(2, result.RetentionTelemetry.ReleaseNotRequiredCount);
            Assert.Equal(result.PayloadValuesPerIteration, result.RetentionTelemetry.RetainedPayloadValueCount);
            Assert.True(result.QueueDrainElapsed >= TimeSpan.Zero);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RebalanceArchiveBenchmarkCacheOverlapUsesSharedQueueAcrossFiles()
    {
        var firstRecord = BuildMessage(31, BuildEightBitType31Payload("REF", [1, 2, 3], scale: 2f, offset: 66f));
        var secondRecord = BuildMessage(31, BuildEightBitType31Payload("VEL", [4, 5], scale: 1f, offset: 8f));
        var compressedPayload1 = BuildFakeBZip2Payload(1);
        var compressedPayload2 = BuildFakeBZip2Payload(2);
        var directory = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        WriteTempFileInDirectory(
            directory,
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload1.Length, compressedPayload1))
                .ToArray());
        WriteTempFileInDirectory(
            directory,
            "KTLX20260504_000846_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload2.Length, compressedPayload2))
                .ToArray());
        WriteTempFileInDirectory(directory, "notes.txt", [1, 2, 3]);
        var benchmark = new RadarProcessingArchiveRebalanceBenchmark(
            new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
            {
                [1] = firstRecord,
                [2] = secondRecord
            }));

        try
        {
            var borrowed = benchmark.MeasureCache(
                directory,
                date: null,
                radarId: null,
                maxFiles: 10,
                mode: RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
                iterations: 1,
                warmupIterations: 0,
                partitionCount: 4,
                shardCount: 2,
                degreeOfParallelism: 2,
                CancellationToken.None);
            var overlap = benchmark.MeasureCache(
                directory,
                date: null,
                radarId: null,
                maxFiles: 10,
                mode: RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
                iterations: 1,
                warmupIterations: 0,
                partitionCount: 4,
                shardCount: 2,
                degreeOfParallelism: 2,
                CancellationToken.None,
                providerMode: RadarProcessingArchiveProviderMode.QueuedOwned,
                queueCapacity: 2,
                providerOverlapMode: RadarProcessingQueuedProviderOverlapMode.ProducerConsumer,
                retentionStrategy: RadarProcessingRetainedPayloadStrategy.PooledCopy,
                queueRetainedPayloadBytes: 4096);

            Assert.Equal(RadarProcessingArchiveProviderMode.QueuedOwned, overlap.ProviderMode);
            Assert.Equal(RadarProcessingQueuedProviderOverlapMode.ProducerConsumer, overlap.ProviderOverlapMode);
            Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, overlap.RetentionStrategy);
            Assert.True(overlap.HasQueueTelemetry);
            Assert.True(overlap.HasRetentionTelemetry);
            Assert.True(overlap.HasOverlapTelemetry);
            Assert.Equal(borrowed.ExaminedFilesPerIteration, overlap.ExaminedFilesPerIteration);
            Assert.Equal(borrowed.SkippedFilesPerIteration, overlap.SkippedFilesPerIteration);
            Assert.Equal(borrowed.PublishedFilesPerIteration, overlap.PublishedFilesPerIteration);
            Assert.Equal(borrowed.BatchesPerIteration, overlap.BatchesPerIteration);
            Assert.Equal(borrowed.EventsPerIteration, overlap.EventsPerIteration);
            Assert.Equal(borrowed.PayloadValuesPerIteration, overlap.PayloadValuesPerIteration);
            Assert.Equal(borrowed.ValidationChecksum, overlap.ValidationChecksum);
            Assert.Equal(2, overlap.QueueTelemetry.EnqueuedBatchCount);
            Assert.Equal(2, overlap.QueueTelemetry.DequeuedBatchCount);
            Assert.Equal(2, overlap.QueueTelemetry.CompletedBatchCount);
            Assert.Equal(2, overlap.RetentionTelemetry.RetentionAttemptCount);
            Assert.Equal(2, overlap.RetentionTelemetry.RetainedBatchCount);
            Assert.Equal(2, overlap.RetentionTelemetry.ReleaseAttemptCount);
            Assert.Equal(2, overlap.RetentionTelemetry.ReleasedBatchCount);
            Assert.Equal(0, overlap.RetentionTelemetry.ReleaseFailedCount);
            Assert.Equal(overlap.QueueTelemetry.EnqueuedBatchCount, overlap.OverlapTelemetry.RetainedBatchCount);
            Assert.Equal(overlap.RetentionTelemetry, overlap.OverlapTelemetry.RetentionTelemetry);
            Assert.Equal(overlap.QueueTelemetry.EnqueuedBatchCount, overlap.OverlapTelemetry.QueueTelemetry.EnqueuedBatchCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RebalanceArchiveBenchmarkControlledConsumerDelayProvesQueuedAheadOverlap()
    {
        var firstRecord = BuildMessage(31, BuildEightBitType31Payload("REF", [1, 2, 3], scale: 2f, offset: 66f));
        var secondRecord = BuildMessage(31, BuildEightBitType31Payload("VEL", [4, 5], scale: 1f, offset: 8f));
        var thirdRecord = BuildMessage(31, BuildEightBitType31Payload("SW", [6, 7, 8], scale: 1f, offset: 4f));
        var compressedPayload1 = BuildFakeBZip2Payload(1);
        var compressedPayload2 = BuildFakeBZip2Payload(2);
        var compressedPayload3 = BuildFakeBZip2Payload(3);
        var directory = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        WriteTempFileInDirectory(
            directory,
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload1.Length, compressedPayload1))
                .ToArray());
        WriteTempFileInDirectory(
            directory,
            "KTLX20260504_000846_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload2.Length, compressedPayload2))
                .ToArray());
        WriteTempFileInDirectory(
            directory,
            "KTLX20260504_001447_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload3.Length, compressedPayload3))
                .ToArray());
        var benchmark = new RadarProcessingArchiveRebalanceBenchmark(
            new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
            {
                [1] = firstRecord,
                [2] = secondRecord,
                [3] = thirdRecord
            }));

        try
        {
            var borrowed = benchmark.MeasureCache(
                directory,
                date: null,
                radarId: null,
                maxFiles: 10,
                mode: RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
                iterations: 1,
                warmupIterations: 0,
                partitionCount: 4,
                shardCount: 2,
                degreeOfParallelism: 2,
                CancellationToken.None);
            var overlap = benchmark.MeasureCache(
                directory,
                date: null,
                radarId: null,
                maxFiles: 10,
                mode: RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
                iterations: 1,
                warmupIterations: 0,
                partitionCount: 4,
                shardCount: 2,
                degreeOfParallelism: 2,
                CancellationToken.None,
                providerMode: RadarProcessingArchiveProviderMode.QueuedOwned,
                queueCapacity: 4,
                providerOverlapMode: RadarProcessingQueuedProviderOverlapMode.ProducerConsumer,
                retentionStrategy: RadarProcessingRetainedPayloadStrategy.PooledCopy,
                queueRetainedPayloadBytes: 4096,
                overlapConsumerDelay: TimeSpan.FromMilliseconds(50));

            Assert.Equal(TimeSpan.FromMilliseconds(50), overlap.OverlapConsumerDelay);
            Assert.Equal(borrowed.PublishedFilesPerIteration, overlap.PublishedFilesPerIteration);
            Assert.Equal(borrowed.BatchesPerIteration, overlap.BatchesPerIteration);
            Assert.Equal(borrowed.ValidationChecksum, overlap.ValidationChecksum);
            Assert.True(overlap.ValidationSucceeded);
            Assert.Equal(3, overlap.QueueTelemetry.EnqueuedBatchCount);
            Assert.Equal(3, overlap.QueueTelemetry.DequeuedBatchCount);
            Assert.Equal(3, overlap.QueueTelemetry.CompletedBatchCount);
            Assert.True(overlap.QueueTelemetry.QueueDepthHighWatermark > 1);
            Assert.True(overlap.OverlapTelemetry.HasQueuedAheadOverlap);
            Assert.Equal(overlap.QueueTelemetry.QueueDepthHighWatermark, overlap.OverlapTelemetry.QueueDepthHighWatermark);
            Assert.Equal(3, overlap.RetentionTelemetry.ReleaseAttemptCount);
            Assert.Equal(3, overlap.RetentionTelemetry.ReleasedBatchCount);
            Assert.Equal(0, overlap.RetentionTelemetry.ReleaseFailedCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static byte[] BuildArchiveTwoHeader()
    {
        var header = new byte[24];
        Encoding.ASCII.GetBytes("AR2V0006.266").CopyTo(header, 0);
        BinaryPrimitives.WriteInt32BigEndian(
            header.AsSpan(12, 4),
            new DateOnly(2026, 5, 4).DayNumber - new DateOnly(1970, 1, 1).DayNumber + 1);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(16, 4), 164_018);
        Encoding.ASCII.GetBytes("KTLX").CopyTo(header, 20);
        return header;
    }

    private static byte[] BuildCompressedRecord(int controlWord, byte[] compressedPayload) =>
        BuildCompressedRecordControlWord(controlWord).Concat(compressedPayload).ToArray();

    private static byte[] BuildCompressedRecordControlWord(int controlWord)
    {
        var buffer = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, controlWord);
        return buffer;
    }

    private static byte[] BuildFakeBZip2Payload(byte key) => [(byte)'B', (byte)'Z', (byte)'h', key];

    private static byte[] BuildMessage(byte messageType, byte[] payload)
    {
        var messageBytes = 16 + payload.Length;
        if (messageBytes % 2 != 0)
        {
            throw new ArgumentException("Synthetic message length must be even.", nameof(payload));
        }

        var message = new byte[messageBytes];
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(0, 2), (ushort)(messageBytes / 2));
        message[2] = 8;
        message[3] = messageType;
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(6, 2), 20_578);
        BinaryPrimitives.WriteUInt32BigEndian(message.AsSpan(8, 4), 164_018);
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(12, 2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(14, 2), 1);
        payload.CopyTo(message.AsSpan(16));
        return message;
    }

    private static byte[] BuildEightBitType31Payload(
        string momentName,
        byte[] values,
        float scale,
        float offset)
    {
        var payload = BuildType31Payload(
            momentName,
            checked((ushort)values.Length),
            wordSizeBits: 8,
            values.Length,
            scale,
            offset);
        values.CopyTo(payload.AsSpan(100));
        return payload;
    }

    private static byte[] BuildSixteenBitType31Payload(
        string momentName,
        ushort[] values,
        float scale,
        float offset)
    {
        var payload = BuildType31Payload(
            momentName,
            checked((ushort)values.Length),
            wordSizeBits: 16,
            values.Length * sizeof(ushort),
            scale,
            offset);
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(100 + i * sizeof(ushort), sizeof(ushort)), values[i]);
        }

        return payload;
    }

    private static byte[] BuildType31Payload(
        string momentName,
        ushort gates,
        byte wordSizeBits,
        int momentDataByteCount,
        float scale,
        float offset)
    {
        const int momentOffset = 72;
        var payload = new byte[Math.Max(momentOffset + 28 + momentDataByteCount, 160)];
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(18, 2), (ushort)payload.Length);
        payload[22] = 1;
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(30, 2), 1);
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(32, 4), momentOffset);

        payload[momentOffset] = (byte)'D';
        for (var i = 0; i < momentName.Length && i < 3; i++)
        {
            payload[momentOffset + 1 + i] = (byte)momentName[i];
        }

        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(momentOffset + 8, 2), gates);
        payload[momentOffset + 19] = wordSizeBits;
        WriteSingleBigEndian(payload.AsSpan(momentOffset + 20, 4), scale);
        WriteSingleBigEndian(payload.AsSpan(momentOffset + 24, 4), offset);
        return payload;
    }

    private static void WriteSingleBigEndian(Span<byte> destination, float value) =>
        BinaryPrimitives.WriteInt32BigEndian(destination, BitConverter.SingleToInt32Bits(value));

    private static string WriteTempFile(string fileName, byte[] contents)
    {
        var directory = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, contents);
        return path;
    }

    private static string WriteTempFileInDirectory(string directory, string fileName, byte[] contents)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, contents);
        return path;
    }

    private sealed class CapturingRadarEventBatchPublisher : IArchiveRadarEventBatchPublisher
    {
        private readonly List<RadarEventBatch> batches = new();

        public IReadOnlyList<RadarEventBatch> Batches => batches;

        public void Publish(RadarEventBatch batch, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            batches.Add(batch.ToOwnedSnapshot());
        }
    }

    private sealed class LeasedCapturingRadarEventBatchPublisher : IArchiveRadarEventBatchPublisher
    {
        private readonly List<RadarEventBatch> batches = new();

        public IReadOnlyList<RadarEventBatch> Batches => batches;

        public void Publish(RadarEventBatch batch, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(RadarEventBatchLifetime.Leased, batch.Lifetime);
            batches.Add(batch.ToOwnedSnapshot());
        }
    }

    private static void AssertArchiveRadarEventBatchPublishTotalsEqual(
        ArchiveRadarEventBatchPublishResult expected,
        ArchiveRadarEventBatchPublishResult actual)
    {
        Assert.Equal(expected.FilePath, actual.FilePath);
        Assert.Equal(expected.Decompressor, actual.Decompressor);
        Assert.Equal(expected.DegreeOfParallelism, actual.DegreeOfParallelism);
        Assert.Equal(expected.FileSizeBytes, actual.FileSizeBytes);
        Assert.Equal(expected.CompressedRecordCount, actual.CompressedRecordCount);
        Assert.Equal(expected.CompressedBytes, actual.CompressedBytes);
        Assert.Equal(expected.DecompressedBytes, actual.DecompressedBytes);
        Assert.Equal(expected.StreamSchemaVersion, actual.StreamSchemaVersion);
        Assert.Equal(expected.DictionaryVersion, actual.DictionaryVersion);
        Assert.Equal(expected.SourceUniverseVersion, actual.SourceUniverseVersion);
        Assert.Equal(expected.BatchCount, actual.BatchCount);
        Assert.Equal(expected.EventCount, actual.EventCount);
        Assert.Equal(expected.PayloadBytes, actual.PayloadBytes);
        Assert.Equal(expected.PayloadValueCount, actual.PayloadValueCount);
        Assert.Equal(expected.RawValueChecksum, actual.RawValueChecksum);
    }

    private sealed class FakeArchiveBZip2Decompressor : IArchiveBZip2Decompressor
    {
        private readonly IReadOnlyDictionary<byte, byte[]> decompressedRecords;
        private readonly IReadOnlyDictionary<byte, int> delayMillisecondsByRecord;

        public FakeArchiveBZip2Decompressor(
            IReadOnlyDictionary<byte, byte[]> decompressedRecords,
            IReadOnlyDictionary<byte, int>? delayMillisecondsByRecord = null)
        {
            this.decompressedRecords = decompressedRecords;
            this.delayMillisecondsByRecord = delayMillisecondsByRecord ?? new Dictionary<byte, int>();
        }

        public string Name => "fake";

        public IArchiveBZip2DecompressionSession CreateSession() =>
            new Session(decompressedRecords, delayMillisecondsByRecord);

        public long Decompress(
            byte[] compressedPayload,
            int compressedSizeBytes,
            byte[] outputBuffer,
            ArchiveBZip2DecompressedChunkHandler? chunkHandler) =>
            CreateSession().Decompress(compressedPayload, compressedSizeBytes, outputBuffer, chunkHandler);

        public long CountDecompressedBytes(byte[] compressedPayload, int compressedSizeBytes, byte[] outputBuffer) =>
            CreateSession().CountDecompressedBytes(compressedPayload, compressedSizeBytes, outputBuffer);

        private sealed class Session : IArchiveBZip2DecompressionSession
        {
            private readonly IReadOnlyDictionary<byte, byte[]> decompressedRecords;
            private readonly IReadOnlyDictionary<byte, int> delayMillisecondsByRecord;

            public Session(
                IReadOnlyDictionary<byte, byte[]> decompressedRecords,
                IReadOnlyDictionary<byte, int> delayMillisecondsByRecord)
            {
                this.decompressedRecords = decompressedRecords;
                this.delayMillisecondsByRecord = delayMillisecondsByRecord;
            }

            public long Decompress(
                byte[] compressedPayload,
                int compressedSizeBytes,
                byte[] outputBuffer,
                ArchiveBZip2DecompressedChunkHandler? chunkHandler)
            {
                var record = ReadRecord(compressedPayload, compressedSizeBytes);
                if (chunkHandler is null)
                {
                    return record.Length;
                }

                var firstChunkLength = Math.Min(5, record.Length);
                chunkHandler(record.AsSpan(0, firstChunkLength));
                if (firstChunkLength < record.Length)
                {
                    chunkHandler(record.AsSpan(firstChunkLength));
                }

                return record.Length;
            }

            public long CountDecompressedBytes(byte[] compressedPayload, int compressedSizeBytes, byte[] outputBuffer) =>
                ReadRecord(compressedPayload, compressedSizeBytes).Length;

            private byte[] ReadRecord(byte[] compressedPayload, int compressedSizeBytes)
            {
                if (compressedSizeBytes < 4 ||
                    compressedPayload[0] != (byte)'B' ||
                    compressedPayload[1] != (byte)'Z' ||
                    compressedPayload[2] != (byte)'h')
                {
                    throw new InvalidDataException("Fake compressed payload does not start with BZh.");
                }

                var recordKey = compressedPayload[3];
                if (delayMillisecondsByRecord.TryGetValue(recordKey, out var delayMilliseconds))
                {
                    Thread.Sleep(delayMilliseconds);
                }

                return decompressedRecords[recordKey];
            }
        }
    }
}
