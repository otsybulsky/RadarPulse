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
                CancellationToken.None,
                providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed);
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
            Assert.Equal(RadarProcessingRetainedResourcePressureSummary.Empty, blocking.RetainedResourcePressure);
            Assert.Equal(0, blocking.ActiveRetainedPayloadBytesHighWatermark);
            Assert.False(blocking.AllocationSummary.IncludesCliFormatting);
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
            Assert.Equal(queued.QueueTelemetry.RetainedResourcePressure, queued.RetainedResourcePressure);
            Assert.Equal(0, queued.CurrentCombinedRetainedBatchCount);
            Assert.Equal(0, queued.CurrentCombinedRetainedPayloadBytes);
            Assert.Equal(queued.PayloadBytesPerIteration, queued.ActiveRetainedPayloadBytesHighWatermark);
            Assert.Equal(queued.PayloadBytesPerIteration, queued.CombinedRetainedPayloadBytesHighWatermark);
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
            Assert.Equal(result.QueueTelemetry.RetainedResourcePressure, result.OverlapTelemetry.RetainedResourcePressure);
            Assert.Equal(result.QueueTelemetry.ActiveRetainedPayloadBytesHighWatermark, result.OverlapTelemetry.ActiveRetainedPayloadBytesHighWatermark);
            Assert.Equal(result.QueueTelemetry.CombinedRetainedPayloadBytesHighWatermark, result.OverlapTelemetry.CombinedRetainedPayloadBytesHighWatermark);
            Assert.Equal(result.QueueTelemetry.RetainedResourcePressure, result.RetainedResourcePressure);
            Assert.Equal(result.PayloadBytesPerIteration, result.ActiveRetainedPayloadBytesHighWatermark);
            Assert.Equal(result.PayloadBytesPerIteration, result.CombinedRetainedPayloadBytesHighWatermark);
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

}
