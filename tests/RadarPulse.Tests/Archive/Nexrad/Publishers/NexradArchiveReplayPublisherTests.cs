using System.Buffers.Binary;
using System.Text;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed class NexradArchiveReplayPublisherTests
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

    [Fact]
    public void ReplayPublishSessionMatchesPublisherTotalsAcrossRepeatedParallelRuns()
    {
        var firstRecordBytes = BuildMessage(31, BuildEightBitType31Payload("REF", [0, 1, 66, 68], scale: 2f, offset: 66f));
        var secondRecordBytes = BuildMessage(31, BuildEightBitType31Payload("VEL", [129, 131], scale: 2f, offset: 129f));
        var compressedPayload1 = BuildFakeBZip2Payload(1);
        var compressedPayload2 = BuildFakeBZip2Payload(2);
        var path = WriteTempFile(
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload1.Length, compressedPayload1))
                .Concat(BuildCompressedRecord(compressedPayload2.Length, compressedPayload2))
                .ToArray());
        var decompressor = new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
        {
            [1] = firstRecordBytes,
            [2] = secondRecordBytes
        });
        var replayPublisher = new NexradArchiveReplayPublisher(decompressor);

        try
        {
            var expected = replayPublisher.PublishFile(
                path,
                ArchiveReplayPublishOptions.Sequential,
                CancellationToken.None);

            using var session = new NexradArchiveReplayPublishSession(decompressor, degreeOfParallelism: 2);
            var first = session.PublishFile(path, CancellationToken.None);
            var second = session.PublishFile(path, CancellationToken.None);

            Assert.Equal(2, first.DegreeOfParallelism);
            Assert.Equal(2, second.DegreeOfParallelism);
            AssertPublishResultsMatch(expected, first);
            AssertPublishResultsMatch(expected, second);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void ReplayPublishSessionRejectsUseAfterDispose()
    {
        var session = new NexradArchiveReplayPublishSession(
            new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>()),
            degreeOfParallelism: 1);

        session.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => session.PublishFile("archive", CancellationToken.None));
        session.Dispose();
    }

    [Fact]
    public void ReplayPublishSessionPublishesSelectedCacheFilesAndSkipsNonArchiveTwoFiles()
    {
        var firstRecordBytes = BuildMessage(31, BuildEightBitType31Payload("REF", [66, 68], scale: 2f, offset: 66f));
        var secondRecordBytes = BuildMessage(31, BuildEightBitType31Payload("VEL", [129, 131], scale: 2f, offset: 129f));
        var compressedPayload1 = BuildFakeBZip2Payload(1);
        var compressedPayload2 = BuildFakeBZip2Payload(2);
        var cachePath = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        var ktlxDirectory = Path.Combine(cachePath, "level2", "2026", "05", "04", "KTLX");
        var kinxDirectory = Path.Combine(cachePath, "level2", "2026", "05", "04", "KINX");
        var nextDayDirectory = Path.Combine(cachePath, "level2", "2026", "05", "05", "KTLX");
        var firstPath = WriteFile(
            ktlxDirectory,
            "KTLX20260504_000100_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload1.Length, compressedPayload1))
                .ToArray());
        var skippedPath = WriteFile(ktlxDirectory, "KTLX20260504_000200_V06_MDM", [0x01, 0x02, 0x03]);
        var secondPath = WriteFile(
            ktlxDirectory,
            "KTLX20260504_000300_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload2.Length, compressedPayload2))
                .ToArray());
        WriteFile(
            kinxDirectory,
            "KINX20260504_000400_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload1.Length, compressedPayload1))
                .ToArray());
        WriteFile(
            nextDayDirectory,
            "KTLX20260505_000500_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload2.Length, compressedPayload2))
                .ToArray());
        var decompressor = new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
        {
            [1] = firstRecordBytes,
            [2] = secondRecordBytes
        });

        try
        {
            using var session = new NexradArchiveReplayPublishSession(decompressor, degreeOfParallelism: 2);
            var result = session.PublishCache(
                cachePath,
                new DateOnly(2026, 5, 4),
                "KTLX",
                maxFiles: 3,
                CancellationToken.None);
            var expectedFirst = new NexradArchiveReplayPublisher(decompressor).PublishFile(
                firstPath,
                ArchiveReplayPublishOptions.Sequential,
                CancellationToken.None);
            var expectedSecond = new NexradArchiveReplayPublisher(decompressor).PublishFile(
                secondPath,
                ArchiveReplayPublishOptions.Sequential,
                CancellationToken.None);

            Assert.Equal(new DirectoryInfo(cachePath).FullName, result.CachePath);
            Assert.Equal(new DateOnly(2026, 5, 4), result.Date);
            Assert.Equal("KTLX", result.RadarId);
            Assert.Equal("fake", result.Decompressor);
            Assert.Equal(2, result.DegreeOfParallelism);
            Assert.Equal(3, result.ExaminedFileCount);
            Assert.Equal(1, result.SkippedFileCount);
            Assert.Equal(2, result.PublishedFileCount);
            Assert.Collection(
                result.Files,
                first => Assert.Equal(firstPath, first.FilePath),
                second => Assert.Equal(secondPath, second.FilePath));
            Assert.DoesNotContain(result.Files, file => file.FilePath == skippedPath);
            Assert.Equal(expectedFirst.FileSizeBytes + expectedSecond.FileSizeBytes, result.TotalFileSizeBytes);
            Assert.Equal(expectedFirst.CompressedRecordCount + expectedSecond.CompressedRecordCount, result.TotalCompressedRecordCount);
            Assert.Equal(expectedFirst.PublishedEvents + expectedSecond.PublishedEvents, result.TotalPublishedEvents);
            Assert.Equal(expectedFirst.ValidEvents + expectedSecond.ValidEvents, result.TotalValidEvents);
            Assert.Equal(expectedFirst.RawValueChecksum + expectedSecond.RawValueChecksum, result.TotalRawValueChecksum);
            Assert.Equal(
                expectedFirst.CalibratedValueScaledChecksum + expectedSecond.CalibratedValueScaledChecksum,
                result.TotalCalibratedValueScaledChecksum);
            Assert.NotEqual(0UL, result.ChronologyChecksum);
        }
        finally
        {
            Directory.Delete(cachePath, recursive: true);
        }
    }

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

    [Fact]
    public void PublishFileRejectsInvalidParallelism()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new NexradArchiveReplayPublisher(new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>()))
                .PublishFile(
                    "archive",
                    new ArchiveReplayPublishOptions(0),
                    CancellationToken.None));

        Assert.Contains("Degree of parallelism", exception.Message);
    }

    [Fact]
    public void ReplayPublishBenchmarkMeasuresStableParallelPublisherIterations()
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

        try
        {
            var result = new NexradArchiveReplayPublishBenchmark(new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
                {
                    [1] = firstRecordBytes,
                    [2] = secondRecordBytes
                }))
                .Measure(
                    path,
                    iterations: 2,
                    warmupIterations: 1,
                    degreeOfParallelism: 2,
                    CancellationToken.None);

            Assert.Equal("fake", result.Decompressor);
            Assert.Equal(2, result.Iterations);
            Assert.Equal(1, result.WarmupIterations);
            Assert.Equal(2, result.DegreeOfParallelism);
            Assert.Equal(2, result.CompressedRecordsPerIteration);
            Assert.Equal(compressedPayload1.Length + compressedPayload2.Length, result.CompressedBytesPerIteration);
            Assert.Equal(firstRecordBytes.Length + secondRecordBytes.Length, result.DecompressedBytesPerIteration);
            Assert.Equal(4, result.PublishedEventsPerIteration);
            Assert.Equal(4, result.ValidEventsPerIteration);
            Assert.Equal(8, result.TotalPublishedEvents);
            Assert.Equal(8, result.TotalValidEvents);
            Assert.Equal(394, result.RawValueChecksumPerIteration);
            Assert.Equal(2_000, result.CalibratedValueScaledChecksumPerIteration);
            Assert.True(result.Elapsed > TimeSpan.Zero);
            Assert.True(result.AllocatedBytes > 0);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void ReplayPublishBenchmarkMeasuresStableCacheIterations()
    {
        var firstRecordBytes = BuildMessage(31, BuildEightBitType31Payload("REF", [66, 68], scale: 2f, offset: 66f));
        var secondRecordBytes = BuildMessage(31, BuildEightBitType31Payload("VEL", [129, 131], scale: 2f, offset: 129f));
        var compressedPayload1 = BuildFakeBZip2Payload(1);
        var compressedPayload2 = BuildFakeBZip2Payload(2);
        var cachePath = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        var ktlxDirectory = Path.Combine(cachePath, "level2", "2026", "05", "04", "KTLX");
        WriteFile(
            ktlxDirectory,
            "KTLX20260504_000100_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload1.Length, compressedPayload1))
                .ToArray());
        WriteFile(ktlxDirectory, "KTLX20260504_000200_V06_MDM", [0x01, 0x02, 0x03]);
        WriteFile(
            ktlxDirectory,
            "KTLX20260504_000300_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload2.Length, compressedPayload2))
                .ToArray());

        try
        {
            var result = new NexradArchiveReplayPublishBenchmark(new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
                {
                    [1] = firstRecordBytes,
                    [2] = secondRecordBytes
                }))
                .MeasureCache(
                    cachePath,
                    new DateOnly(2026, 5, 4),
                    "KTLX",
                    maxFiles: 3,
                    iterations: 2,
                    warmupIterations: 1,
                    degreeOfParallelism: 2,
                    CancellationToken.None);

            Assert.Equal(new DirectoryInfo(cachePath).FullName, result.CachePath);
            Assert.Equal(new DateOnly(2026, 5, 4), result.Date);
            Assert.Equal("KTLX", result.RadarId);
            Assert.Equal("fake", result.Decompressor);
            Assert.Equal(2, result.Iterations);
            Assert.Equal(1, result.WarmupIterations);
            Assert.Equal(2, result.DegreeOfParallelism);
            Assert.Equal(3, result.ExaminedFilesPerIteration);
            Assert.Equal(1, result.SkippedFilesPerIteration);
            Assert.Equal(2, result.PublishedFilesPerIteration);
            Assert.Equal(2, result.CompressedRecordsPerIteration);
            Assert.Equal(compressedPayload1.Length + compressedPayload2.Length, result.CompressedBytesPerIteration);
            Assert.Equal(firstRecordBytes.Length + secondRecordBytes.Length, result.DecompressedBytesPerIteration);
            Assert.Equal(4, result.PublishedEventsPerIteration);
            Assert.Equal(4, result.ValidEventsPerIteration);
            Assert.Equal(8, result.TotalPublishedEvents);
            Assert.Equal(8, result.TotalValidEvents);
            Assert.Equal(394, result.RawValueChecksumPerIteration);
            Assert.Equal(2_000, result.CalibratedValueScaledChecksumPerIteration);
            Assert.NotEqual(0UL, result.ChronologyChecksumPerIteration);
            Assert.True(result.Elapsed > TimeSpan.Zero);
            Assert.True(result.AllocatedBytes > 0);
        }
        finally
        {
            Directory.Delete(cachePath, recursive: true);
        }
    }

    [Fact]
    public void PublishFileRejectsNonArchiveTwoFile()
    {
        var path = WriteTempFile("unknown", [0x01, 0x02, 0x03, 0x04]);
        try
        {
            var exception = Assert.Throws<InvalidDataException>(
                () => new NexradArchiveReplayPublisher(new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>()))
                    .PublishFile(path, ArchiveReplayPublishOptions.Sequential, CancellationToken.None));

            Assert.Contains("Archive Two volume header", exception.Message);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void PublishFileHonorsCancellation()
    {
        var compressedPayload = BuildFakeBZip2Payload(1);
        var path = WriteTempFile(
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload.Length, compressedPayload))
                .ToArray());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            Assert.Throws<OperationCanceledException>(
                () => new NexradArchiveReplayPublisher(new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
                    {
                        [1] = BuildMessage(31, BuildEightBitType31Payload("REF", [66]))
                    }))
                    .PublishFile(path, ArchiveReplayPublishOptions.Sequential, cancellation.Token));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
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
        float scale = 2f,
        float offset = 66f)
    {
        var payload = BuildType31Payload(
            momentName,
            checked((ushort)values.Length),
            wordSizeBits: 8,
            values.Length,
            scale: scale,
            offset: offset);
        values.CopyTo(payload.AsSpan(100));
        return payload;
    }

    private static byte[] BuildType31Payload(
        string momentName,
        ushort gates,
        byte wordSizeBits,
        int momentDataByteCount,
        float firstGateRangeKilometers = 0.3f,
        float gateSpacingKilometers = 0.25f,
        float scale = 2f,
        float offset = 66f)
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
        WriteScaledKilometers(payload.AsSpan(momentOffset + 10, 2), firstGateRangeKilometers);
        WriteScaledKilometers(payload.AsSpan(momentOffset + 12, 2), gateSpacingKilometers);
        payload[momentOffset + 19] = wordSizeBits;
        WriteSingleBigEndian(payload.AsSpan(momentOffset + 20, 4), scale);
        WriteSingleBigEndian(payload.AsSpan(momentOffset + 24, 4), offset);
        return payload;
    }

    private static void WriteSingleBigEndian(Span<byte> destination, float value) =>
        BinaryPrimitives.WriteInt32BigEndian(destination, BitConverter.SingleToInt32Bits(value));

    private static void WriteScaledKilometers(Span<byte> destination, float kilometers) =>
        BinaryPrimitives.WriteUInt16BigEndian(destination, checked((ushort)MathF.Round(kilometers * 1_000f)));

    private static void AssertPublishResultsMatch(
        ArchiveReplayPublishResult expected,
        ArchiveReplayPublishResult actual)
    {
        Assert.Equal(expected.FilePath, actual.FilePath);
        Assert.Equal(expected.Decompressor, actual.Decompressor);
        Assert.Equal(expected.FileSizeBytes, actual.FileSizeBytes);
        Assert.Equal(expected.CompressedRecordCount, actual.CompressedRecordCount);
        Assert.Equal(expected.CompressedBytes, actual.CompressedBytes);
        Assert.Equal(expected.DecompressedBytes, actual.DecompressedBytes);
        Assert.Equal(expected.PublishedEvents, actual.PublishedEvents);
        Assert.Equal(expected.ValidEvents, actual.ValidEvents);
        Assert.Equal(expected.BelowThresholdEvents, actual.BelowThresholdEvents);
        Assert.Equal(expected.RangeFoldedEvents, actual.RangeFoldedEvents);
        Assert.Equal(expected.ClutterFilterNotAppliedEvents, actual.ClutterFilterNotAppliedEvents);
        Assert.Equal(expected.PointClutterFilterAppliedEvents, actual.PointClutterFilterAppliedEvents);
        Assert.Equal(expected.DualPolarizationFilteredEvents, actual.DualPolarizationFilteredEvents);
        Assert.Equal(expected.ReservedEvents, actual.ReservedEvents);
        Assert.Equal(expected.UnsupportedEvents, actual.UnsupportedEvents);
        Assert.Equal(expected.RawValueChecksum, actual.RawValueChecksum);
        Assert.Equal(expected.CalibratedValueScaledChecksum, actual.CalibratedValueScaledChecksum);
        Assert.Equal(expected.ChronologyChecksum, actual.ChronologyChecksum);
    }

    private static string WriteTempFile(string fileName, byte[] contents)
    {
        var directory = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        return WriteFile(directory, fileName, contents);
    }

    private static string WriteFile(string directory, string fileName, byte[] contents)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, contents);
        return path;
    }

    private sealed class CapturingReplayPublisher : IArchiveReplayEventPublisher
    {
        private readonly List<ArchiveTwoGateMomentEvent> events = new();

        public IReadOnlyList<ArchiveTwoGateMomentEvent> Events => events;

        public void Publish(ArchiveTwoGateMomentEvent gateMomentEvent, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            events.Add(gateMomentEvent);
        }
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
                if (delayMillisecondsByRecord.TryGetValue(recordKey, out var delayMilliseconds) &&
                    delayMilliseconds > 0)
                {
                    Thread.Sleep(delayMilliseconds);
                }

                return decompressedRecords[recordKey];
            }
        }
    }
}
