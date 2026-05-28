using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingCorePartitionedBarrierTests
{
    [Fact]
    public void PartitionedBarrierPreservesSameSourceOrder()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = CreatePartitionedCore(universe, partitionCount: 4, shardCount: 2);
        var batch = CreateBatch(
            universe.Version,
            new[]
            {
                CreateEvent(sourceId: 1, messageTimestampUtcTicks: 100, payloadOffset: 0),
                CreateEvent(sourceId: 3, messageTimestampUtcTicks: 101, payloadOffset: 4),
                CreateEvent(sourceId: 1, messageTimestampUtcTicks: 102, payloadOffset: 8),
                CreateEvent(sourceId: 1, messageTimestampUtcTicks: 103, payloadOffset: 12)
            },
            new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });

        var result = core.Process(batch);
        var sourceOne = core.GetSourceSnapshot(sourceId: 1);

        Assert.True(result.IsValid);
        Assert.Equal(3, sourceOne.ProcessedEventCount);
        Assert.Equal(103, sourceOne.LastMessageTimestampUtcTicks);
        Assert.Equal(110, sourceOne.RawValueChecksum);
    }
}
