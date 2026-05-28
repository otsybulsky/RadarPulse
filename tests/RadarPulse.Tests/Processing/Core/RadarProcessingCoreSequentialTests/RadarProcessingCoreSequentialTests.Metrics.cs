using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingCoreSequentialTests
{
    [Fact]
    public void EmptyBatchProducesDeterministicZeroEventResult()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = new RadarProcessingCore(universe);

        var result = core.Process(CreateEmptyBatch(universe.Version));

        Assert.True(result.IsValid);
        Assert.Equal(RadarProcessingExecutionMode.Sequential, result.ExecutionMode);
        Assert.Equal(1, result.PartitionCount);
        Assert.Equal(1, result.ShardCount);
        Assert.Equal(1, result.Metrics.ProcessedBatchCount);
        Assert.Equal(0, result.Metrics.ProcessedStreamEventCount);
        Assert.Equal(0, result.Metrics.ProcessedPayloadValueCount);
        Assert.Equal(0, result.Metrics.ActiveSourceCount);
        Assert.Equal(0, result.Metrics.RawValueChecksum);
        Assert.Equal(0UL, result.Metrics.ProcessingChecksum);
        Assert.Equal(result.Metrics, result.Validation.Metrics);
    }

    [Fact]
    public void SequentialProcessingUpdatesMetricsAndSourceSnapshots()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var core = new RadarProcessingCore(universe);
        var events = new[]
        {
            CreateEvent(
                sourceId: 0,
                messageTimestampUtcTicks: 100,
                payloadOffset: 0,
                gateCount: 4,
                wordSize: RadarStreamWordSize.EightBit),
            CreateEvent(
                sourceId: 1,
                messageTimestampUtcTicks: 101,
                payloadOffset: 4,
                gateCount: 2,
                wordSize: RadarStreamWordSize.SixteenBit)
        };
        var payload = new byte[] { 1, 2, 3, 4, 0, 5, 1, 0 };
        var batch = CreateBatch(universe.Version, events, payload);
        var batchMetrics = RadarEventBatchMetrics.Compute(batch);

        var result = core.Process(batch);

        Assert.True(result.IsValid);
        Assert.Equal(1, result.Metrics.ProcessedBatchCount);
        Assert.Equal(batchMetrics.EventCount, result.Metrics.ProcessedStreamEventCount);
        Assert.Equal(batchMetrics.PayloadValueCount, result.Metrics.ProcessedPayloadValueCount);
        Assert.Equal(batchMetrics.RawValueChecksum, result.Metrics.RawValueChecksum);
        Assert.Equal(2, result.Metrics.ActiveSourceCount);
        Assert.NotEqual(0UL, result.Metrics.ProcessingChecksum);
        Assert.Equal(result.Metrics, core.CreateMetrics());

        var sourceZero = core.GetSourceSnapshot(sourceId: 0);
        Assert.True(sourceZero.IsActive);
        Assert.Equal(1, sourceZero.ProcessedEventCount);
        Assert.Equal(4, sourceZero.ProcessedPayloadValueCount);
        Assert.Equal(10, sourceZero.RawValueChecksum);
        Assert.Equal(100, sourceZero.LastMessageTimestampUtcTicks);
        Assert.NotEqual(0UL, sourceZero.ProcessingChecksum);

        var sourceOne = core.GetSourceSnapshot(sourceId: 1);
        Assert.True(sourceOne.IsActive);
        Assert.Equal(1, sourceOne.ProcessedEventCount);
        Assert.Equal(2, sourceOne.ProcessedPayloadValueCount);
        Assert.Equal(261, sourceOne.RawValueChecksum);
        Assert.Equal(101, sourceOne.LastMessageTimestampUtcTicks);
        Assert.NotEqual(0UL, sourceOne.ProcessingChecksum);
    }

    [Fact]
    public void SequentialProcessingAccumulatesAcrossBatches()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = new RadarProcessingCore(universe);
        var first = CreateBatch(
            universe.Version,
            new[] { CreateEvent(sourceId: 0, messageTimestampUtcTicks: 100, payloadOffset: 0) },
            new byte[] { 1, 2, 3, 4 });
        var second = CreateBatch(
            universe.Version,
            new[] { CreateEvent(sourceId: 0, messageTimestampUtcTicks: 101, payloadOffset: 0) },
            new byte[] { 5, 6, 7, 8 });

        core.Process(first);
        var result = core.Process(second);

        Assert.Equal(2, result.Metrics.ProcessedBatchCount);
        Assert.Equal(2, result.Metrics.ProcessedStreamEventCount);
        Assert.Equal(8, result.Metrics.ProcessedPayloadValueCount);
        Assert.Equal(36, result.Metrics.RawValueChecksum);
        Assert.Equal(1, result.Metrics.ActiveSourceCount);
    }
}
