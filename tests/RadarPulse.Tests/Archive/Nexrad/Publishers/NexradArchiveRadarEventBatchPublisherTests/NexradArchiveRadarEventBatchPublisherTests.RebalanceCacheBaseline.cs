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
                CancellationToken.None,
                providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed);
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
                asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 1),
                providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed);

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
    public void RebalanceArchiveBenchmarkCacheAutoSizesSourceUniverseForMixedRadars()
    {
        var kinxRecord = BuildMessage(
            31,
            BuildEightBitType31Payload("REF", [1, 2, 3], scale: 2f, offset: 66f),
            millisecondsPastMidnight: 86_000_000);
        var ktlxRecord = BuildMessage(
            31,
            BuildEightBitType31Payload("REF", [4, 5, 6], scale: 2f, offset: 66f),
            millisecondsPastMidnight: 164_018);
        var compressedPayload1 = BuildFakeBZip2Payload(1);
        var compressedPayload2 = BuildFakeBZip2Payload(2);
        var directory = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        WriteTempFileInDirectory(
            directory,
            "KINX20260504_235749_V06",
            BuildArchiveTwoHeader("KINX", millisecondsPastMidnight: 86_000_000)
                .Concat(BuildCompressedRecord(compressedPayload1.Length, compressedPayload1))
                .ToArray());
        WriteTempFileInDirectory(
            directory,
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader("KTLX", millisecondsPastMidnight: 164_018)
                .Concat(BuildCompressedRecord(compressedPayload2.Length, compressedPayload2))
                .ToArray());
        var benchmark = new RadarProcessingArchiveRebalanceBenchmark(
            new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
            {
                [1] = kinxRecord,
                [2] = ktlxRecord
            }));

        try
        {
            var mixed = benchmark.MeasureCache(
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
                executionMode: RadarProcessingExecutionMode.AsyncShardTransport,
                asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 1),
                providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed);
            var filtered = benchmark.MeasureCache(
                directory,
                date: null,
                radarId: "KTLX",
                maxFiles: 10,
                mode: RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
                iterations: 1,
                warmupIterations: 0,
                partitionCount: 4,
                shardCount: 2,
                degreeOfParallelism: 1,
                CancellationToken.None,
                providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed);

            var singleRadarSourceCount = ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse.SourceCount;
            Assert.Equal(singleRadarSourceCount * 2, mixed.SourceCount);
            Assert.Equal(singleRadarSourceCount, filtered.SourceCount);
            Assert.Equal(2, mixed.PublishedFilesPerIteration);
            Assert.Equal(1, filtered.PublishedFilesPerIteration);
            Assert.True(mixed.ValidationSucceeded);
            Assert.True(mixed.ProcessingSucceeded);
            Assert.Equal(0, mixed.ProcessingValidationFailedBatchCount);
            Assert.Equal(0, mixed.WorkerFailedBatchCount);
            Assert.Equal(0, mixed.WorkerFailedWorkItemCount);
            Assert.NotNull(mixed.WorkerTelemetry);
            Assert.Equal(2, mixed.WorkerTelemetry.Counters.CompletedBatchCount);
            Assert.Equal(0, mixed.WorkerTelemetry.Counters.FailedBatchCount);
            Assert.Equal(0, mixed.WorkerTelemetry.Counters.FailedWorkItemCount);
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
            Assert.Equal(result.QueueTelemetry.RetainedResourcePressure, result.RetainedResourcePressure);
            Assert.Equal(0, result.CurrentCombinedRetainedBatchCount);
            Assert.Equal(0, result.CurrentCombinedRetainedPayloadBytes);
            Assert.True(result.ActiveRetainedPayloadBytesHighWatermark > 0);
            Assert.True(result.CombinedRetainedPayloadBytesHighWatermark >= result.ActiveRetainedPayloadBytesHighWatermark);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

}
