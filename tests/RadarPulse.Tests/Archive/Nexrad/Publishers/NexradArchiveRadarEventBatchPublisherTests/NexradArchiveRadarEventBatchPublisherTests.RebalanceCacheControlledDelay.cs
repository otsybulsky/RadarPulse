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
