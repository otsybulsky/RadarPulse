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
    public void RebalanceArchiveBenchmarkFileUsesRolloutDefaultAndPreservesBorrowedFallback()
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
            var directDefault = benchmark.MeasureFile(
                path,
                RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
                iterations: 1,
                warmupIterations: 0,
                partitionCount: 4,
                shardCount: 2,
                degreeOfParallelism: 1,
                CancellationToken.None);
            var borrowed = benchmark.MeasureFile(
                path,
                RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
                iterations: 1,
                warmupIterations: 0,
                partitionCount: 4,
                shardCount: 2,
                degreeOfParallelism: 1,
                CancellationToken.None,
                providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed);
            var rollout = benchmark.MeasureFile(
                path,
                RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
                iterations: 1,
                warmupIterations: 0,
                partitionCount: 4,
                shardCount: 2,
                degreeOfParallelism: 1,
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
            Assert.Equal(borrowed.BatchesPerIteration, rollout.BatchesPerIteration);
            Assert.Equal(borrowed.EventsPerIteration, rollout.EventsPerIteration);
            Assert.Equal(borrowed.PayloadValuesPerIteration, rollout.PayloadValuesPerIteration);
            Assert.Equal(borrowed.RawValueChecksumPerIteration, rollout.RawValueChecksumPerIteration);
            Assert.Equal(borrowed.TopologyVersionCount, rollout.TopologyVersionCount);
            Assert.Equal(borrowed.RebalanceEvaluationCount, rollout.RebalanceEvaluationCount);
            Assert.Equal(borrowed.AcceptedMoveCount, rollout.AcceptedMoveCount);
            Assert.Equal(borrowed.SkippedDecisionCount, rollout.SkippedDecisionCount);
            Assert.Equal(borrowed.FailedMigrationCount, rollout.FailedMigrationCount);
            Assert.Equal(borrowed.ValidationSucceeded, rollout.ValidationSucceeded);
            Assert.Equal(borrowed.ValidationChecksum, rollout.ValidationChecksum);
            Assert.Equal(borrowed.SkippedReasonCounters, rollout.SkippedReasonCounters);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void RebalanceArchiveBenchmarkFileCanUsePrewarmedRetainedPayloadFactory()
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
        var eventPool = new RadarProcessingRetainedEventArrayPool(
            largeArrayThreshold: 1,
            maxRetainedArrayCount: 2,
            maxRetainedBytes: 128 * RadarStreamEvent.SizeInBytes);
        var payloadPool = new RadarProcessingRetainedPayloadByteArrayPool(
            largeArrayThreshold: 1,
            maxRetainedArrayCount: 2,
            maxRetainedBytes: 128);
        var retainedPayloadFactory = new RadarProcessingRetainedPayloadFactory(eventPool, payloadPool);
        var prewarm = retainedPayloadFactory.Prewarm(eventCount: 1, payloadBytes: 3);

        try
        {
            var result = benchmark.MeasureFile(
                path,
                RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
                iterations: 1,
                warmupIterations: 0,
                partitionCount: 4,
                shardCount: 2,
                degreeOfParallelism: 1,
                CancellationToken.None,
                retainedPayloadFactory: retainedPayloadFactory);

            AssertDirectQueuedOwnedRolloutContour(result);
            Assert.False(result.HasRetainedPayloadPrewarm);
            Assert.True(prewarm.AllocatedBytes > 0);
            Assert.Equal(1, result.RetentionTelemetry.EventPoolRentCount);
            Assert.Equal(1, result.RetentionTelemetry.PayloadPoolRentCount);
            Assert.Equal(0, result.RetentionTelemetry.PoolMissCount);
            Assert.Equal(0, result.RetentionTelemetry.EventPoolMissCount);
            Assert.Equal(0, result.RetentionTelemetry.PayloadPoolMissCount);
            Assert.Equal(0, eventPool.MissCount);
            Assert.Equal(0, payloadPool.MissCount);
            Assert.True(result.ValidationSucceeded);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void RebalanceArchiveBenchmarkRolloutDefaultContractPinsAcceptedContour()
    {
        Assert.Equal(RadarProcessingArchiveProviderMode.QueuedOwned, RadarProcessingArchiveRebalanceRolloutDefaults.ProviderMode);
        Assert.Equal(
            RadarProcessingQueuedProviderOverlapMode.ProducerConsumer,
            RadarProcessingArchiveRebalanceRolloutDefaults.ProviderOverlapMode);
        Assert.Equal(
            RadarProcessingRetainedPayloadStrategy.PooledCopy,
            RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy);
        Assert.Equal(
            RadarProcessingExecutionMode.AsyncShardTransport,
            RadarProcessingArchiveRebalanceRolloutDefaults.ExecutionMode);
        Assert.Equal(4, RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount);
        Assert.Equal(8, RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity);
        Assert.True(RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmEnabled);
        Assert.Equal(65_536, RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmEventCount);
        Assert.Equal(64 * 1024 * 1024, RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmPayloadBytes);
        Assert.Equal(1, RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmBatchCount);
        Assert.Equal(8, RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity);
        Assert.Equal(536_870_912, RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes);
        Assert.Equal(TimeSpan.Zero, RadarProcessingArchiveRebalanceRolloutDefaults.OverlapConsumerDelay);

        var asyncExecution = RadarProcessingArchiveRebalanceRolloutDefaults.CreateAsyncExecution();

        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount, asyncExecution.WorkerCount);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity, asyncExecution.QueueCapacity);
        Assert.True(
            RadarProcessingArchiveRebalanceRolloutDefaults.Matches(
                RadarProcessingArchiveRebalanceRolloutDefaults.ProviderMode,
                RadarProcessingArchiveRebalanceRolloutDefaults.ProviderOverlapMode,
                RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy,
                RadarProcessingArchiveRebalanceRolloutDefaults.ExecutionMode,
                asyncExecution,
                RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity,
                RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes,
                RadarProcessingArchiveRebalanceRolloutDefaults.OverlapConsumerDelay));
    }

}
