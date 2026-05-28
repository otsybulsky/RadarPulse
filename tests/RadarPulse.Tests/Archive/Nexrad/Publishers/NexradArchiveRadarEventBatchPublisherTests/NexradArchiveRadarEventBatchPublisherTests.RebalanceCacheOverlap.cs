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
    public void RebalanceArchiveBenchmarkCacheUsesRolloutDefaultAndPreservesBorrowedFallback()
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
            var directDefault = benchmark.MeasureCache(
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
                CancellationToken.None,
                providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed);
            var rollout = benchmark.MeasureCache(
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
                executionMode: RadarProcessingArchiveRebalanceRolloutDefaults.ExecutionMode,
                asyncExecution: RadarProcessingArchiveRebalanceRolloutDefaults.CreateAsyncExecution(),
                providerMode: RadarProcessingArchiveRebalanceRolloutDefaults.ProviderMode,
                queueCapacity: RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity,
                providerOverlapMode: RadarProcessingArchiveRebalanceRolloutDefaults.ProviderOverlapMode,
                retentionStrategy: RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy,
                queueRetainedPayloadBytes: RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes);

            AssertDirectQueuedOwnedRolloutContour(directDefault);
            AssertDirectBorrowedDefaultContour(borrowed);
            AssertDirectQueuedOwnedRolloutContour(rollout);
            AssertDefaultRetainedPayloadPrewarm(directDefault);
            Assert.False(borrowed.HasRetainedPayloadPrewarm);
            AssertDefaultRetainedPayloadPrewarm(rollout);
            Assert.Equal(directDefault.ExaminedFilesPerIteration, rollout.ExaminedFilesPerIteration);
            Assert.Equal(directDefault.SkippedFilesPerIteration, rollout.SkippedFilesPerIteration);
            Assert.Equal(directDefault.PublishedFilesPerIteration, rollout.PublishedFilesPerIteration);
            Assert.Equal(directDefault.BatchesPerIteration, rollout.BatchesPerIteration);
            Assert.Equal(directDefault.EventsPerIteration, rollout.EventsPerIteration);
            Assert.Equal(directDefault.PayloadValuesPerIteration, rollout.PayloadValuesPerIteration);
            Assert.Equal(directDefault.RawValueChecksumPerIteration, rollout.RawValueChecksumPerIteration);
            Assert.Equal(directDefault.TopologyVersionCount, rollout.TopologyVersionCount);
            Assert.Equal(directDefault.RebalanceEvaluationCount, rollout.RebalanceEvaluationCount);
            Assert.Equal(directDefault.AcceptedMoveCount, rollout.AcceptedMoveCount);
            Assert.Equal(directDefault.SkippedDecisionCount, rollout.SkippedDecisionCount);
            Assert.Equal(directDefault.FailedMigrationCount, rollout.FailedMigrationCount);
            Assert.Equal(directDefault.ValidationSucceeded, rollout.ValidationSucceeded);
            Assert.Equal(directDefault.ValidationChecksum, rollout.ValidationChecksum);
            Assert.Equal(directDefault.SkippedReasonCounters, rollout.SkippedReasonCounters);
            Assert.Equal(2, rollout.QueueTelemetry.EnqueuedBatchCount);
            Assert.Equal(2, rollout.QueueTelemetry.DequeuedBatchCount);
            Assert.Equal(2, rollout.QueueTelemetry.CompletedBatchCount);
            Assert.Equal(2, rollout.RetentionTelemetry.RetentionAttemptCount);
            Assert.Equal(2, rollout.RetentionTelemetry.RetainedBatchCount);
            Assert.Equal(2, rollout.RetentionTelemetry.ReleaseAttemptCount);
            Assert.Equal(2, rollout.RetentionTelemetry.ReleasedBatchCount);
            Assert.Equal(0, rollout.RetentionTelemetry.ReleaseFailedCount);
            Assert.Equal(rollout.QueueTelemetry.RetainedResourcePressure, rollout.OverlapTelemetry.RetainedResourcePressure);
            Assert.Equal(borrowed.PublishedFilesPerIteration, rollout.PublishedFilesPerIteration);
            Assert.Equal(borrowed.BatchesPerIteration, rollout.BatchesPerIteration);
            Assert.Equal(borrowed.EventsPerIteration, rollout.EventsPerIteration);
            Assert.Equal(borrowed.PayloadValuesPerIteration, rollout.PayloadValuesPerIteration);
            Assert.Equal(borrowed.ValidationChecksum, rollout.ValidationChecksum);
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
                CancellationToken.None,
                providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed);
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
            Assert.Equal(overlap.QueueTelemetry.RetainedResourcePressure, overlap.OverlapTelemetry.RetainedResourcePressure);
            Assert.Equal(overlap.QueueTelemetry.RetainedResourcePressure, overlap.RetainedResourcePressure);
            Assert.Equal(overlap.QueueTelemetry.PendingRetainedPayloadBytesHighWatermark, overlap.OverlapTelemetry.PendingRetainedPayloadBytesHighWatermark);
            Assert.Equal(overlap.QueueTelemetry.ActiveRetainedPayloadBytesHighWatermark, overlap.OverlapTelemetry.ActiveRetainedPayloadBytesHighWatermark);
            Assert.Equal(overlap.QueueTelemetry.CombinedRetainedPayloadBytesHighWatermark, overlap.OverlapTelemetry.CombinedRetainedPayloadBytesHighWatermark);
            Assert.True(overlap.ActiveRetainedPayloadBytesHighWatermark > 0);
            Assert.True(overlap.CombinedRetainedPayloadBytesHighWatermark >= overlap.ActiveRetainedPayloadBytesHighWatermark);
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
                CancellationToken.None,
                providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed);
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
            Assert.Equal(overlap.QueueTelemetry.RetainedResourcePressure, overlap.OverlapTelemetry.RetainedResourcePressure);
            Assert.True(overlap.OverlapTelemetry.ActiveRetainedPayloadBytesHighWatermark > 0);
            Assert.True(overlap.OverlapTelemetry.CombinedRetainedPayloadBytesHighWatermark >= overlap.OverlapTelemetry.ActiveRetainedPayloadBytesHighWatermark);
            Assert.Equal(3, overlap.RetentionTelemetry.ReleaseAttemptCount);
            Assert.Equal(3, overlap.RetentionTelemetry.ReleasedBatchCount);
            Assert.Equal(0, overlap.RetentionTelemetry.ReleaseFailedCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

}
