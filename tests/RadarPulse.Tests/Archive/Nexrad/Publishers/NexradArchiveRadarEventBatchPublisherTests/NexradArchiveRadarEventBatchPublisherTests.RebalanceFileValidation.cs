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
                    providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed,
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
                    providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed,
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

}
