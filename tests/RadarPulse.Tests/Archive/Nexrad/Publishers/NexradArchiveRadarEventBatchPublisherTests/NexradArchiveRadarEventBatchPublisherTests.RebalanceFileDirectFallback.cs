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
    public void RebalanceArchiveBenchmarkDirectBorrowedFallbackOmitsQueuedTelemetry()
    {
        var record = BuildMessage(31, BuildEightBitType31Payload("REF", [1, 2, 3], scale: 2f, offset: 66f));
        var compressedPayload = BuildFakeBZip2Payload(1);
        var directory = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = WriteTempFileInDirectory(
            directory,
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload.Length, compressedPayload))
                .ToArray());
        WriteTempFileInDirectory(directory, "notes.txt", [1, 2, 3]);
        var benchmark = new RadarProcessingArchiveRebalanceBenchmark(
            new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
            {
                [1] = record
            }));

        try
        {
            var fileResult = benchmark.MeasureFile(
                path,
                RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
                iterations: 1,
                warmupIterations: 0,
                partitionCount: 4,
                shardCount: 2,
                degreeOfParallelism: 1,
                CancellationToken.None,
                providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed);
            var cacheResult = benchmark.MeasureCache(
                directory,
                date: null,
                radarId: null,
                maxFiles: 10,
                mode: RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
                iterations: 1,
                warmupIterations: 0,
                partitionCount: 4,
                shardCount: 2,
                degreeOfParallelism: 1,
                CancellationToken.None,
                providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed);

            AssertDirectBorrowedDefaultContour(fileResult);
            AssertDirectBorrowedDefaultContour(cacheResult);
            Assert.Equal(fileResult.BatchesPerIteration, cacheResult.BatchesPerIteration);
            Assert.Equal(fileResult.EventsPerIteration, cacheResult.EventsPerIteration);
            Assert.Equal(fileResult.PayloadValuesPerIteration, cacheResult.PayloadValuesPerIteration);
            Assert.Equal(fileResult.ValidationChecksum, cacheResult.ValidationChecksum);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RebalanceArchiveBenchmarkDirectDefaultFailureDoesNotFallbackToBorrowed()
    {
        var record = BuildMessage(31, BuildEightBitType31Payload("REF", [1, 2, 3], scale: 2f, offset: 66f));
        var compressedPayload = BuildFakeBZip2Payload(1);
        var directory = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = WriteTempFileInDirectory(
            directory,
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
            var fileFailure = Assert.Throws<InvalidOperationException>(() =>
                benchmark.MeasureFile(
                    path,
                    RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
                    iterations: 1,
                    warmupIterations: 0,
                    partitionCount: 4,
                    shardCount: 2,
                    degreeOfParallelism: 1,
                    CancellationToken.None,
                    queueRetainedPayloadBytes: 1));
            var cacheFailure = Assert.Throws<InvalidOperationException>(() =>
                benchmark.MeasureCache(
                    directory,
                    date: null,
                    radarId: null,
                    maxFiles: 10,
                    mode: RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
                    iterations: 1,
                    warmupIterations: 0,
                    partitionCount: 4,
                    shardCount: 2,
                    degreeOfParallelism: 1,
                    CancellationToken.None,
                    queueRetainedPayloadBytes: 1));
            var borrowedFile = benchmark.MeasureFile(
                path,
                RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
                iterations: 1,
                warmupIterations: 0,
                partitionCount: 4,
                shardCount: 2,
                degreeOfParallelism: 1,
                CancellationToken.None,
                providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed);
            var borrowedCache = benchmark.MeasureCache(
                directory,
                date: null,
                radarId: null,
                maxFiles: 10,
                mode: RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
                iterations: 1,
                warmupIterations: 0,
                partitionCount: 4,
                shardCount: 2,
                degreeOfParallelism: 1,
                CancellationToken.None,
                providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed);

            Assert.Contains("retained payload byte budget", fileFailure.Message, StringComparison.Ordinal);
            Assert.Contains("retained payload byte budget", cacheFailure.Message, StringComparison.Ordinal);
            AssertDirectBorrowedDefaultContour(borrowedFile);
            AssertDirectBorrowedDefaultContour(borrowedCache);
            Assert.True(borrowedFile.ValidationSucceeded);
            Assert.True(borrowedCache.ValidationSucceeded);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RebalanceArchiveBenchmarkDirectDefaultRejectsBuilderTransfer()
    {
        var record = BuildMessage(31, BuildEightBitType31Payload("REF", [1, 2, 3], scale: 2f, offset: 66f));
        var compressedPayload = BuildFakeBZip2Payload(1);
        var directory = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = WriteTempFileInDirectory(
            directory,
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
                    retentionStrategy: RadarProcessingRetainedPayloadStrategy.BuilderTransfer));
            Assert.Throws<NotSupportedException>(() =>
                benchmark.MeasureCache(
                    directory,
                    date: null,
                    radarId: null,
                    maxFiles: 10,
                    mode: RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
                    iterations: 1,
                    warmupIterations: 0,
                    partitionCount: 4,
                    shardCount: 2,
                    degreeOfParallelism: 1,
                    CancellationToken.None,
                    retentionStrategy: RadarProcessingRetainedPayloadStrategy.BuilderTransfer));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

}
