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
}
