using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingBatchRouterTests
{
    [Fact]
    public void RouterRejectsNullInputsAndSourceUniverseVersionMismatch()
    {
        var topology = CreateTopology(sourceCount: 2, partitionCount: 1, shardCount: 1);
        var router = new RadarProcessingBatchRouter(topology);
        var mismatchedBatch = CreateEightBitBatch(
            new SourceUniverseVersion(2),
            sourceIds: [0]);

        Assert.Throws<ArgumentNullException>(() => new RadarProcessingBatchRouter(null!));
        Assert.Throws<ArgumentNullException>(() => router.Route(null!));
        Assert.Throws<ArgumentException>(() => router.Route(mismatchedBatch));
    }

    [Fact]
    public void RouterRejectsSourceIdOutsideTopologyBeforeReturningRoute()
    {
        var topology = CreateTopology(sourceCount: 2, partitionCount: 1, shardCount: 1);
        var router = new RadarProcessingBatchRouter(topology);
        var batch = CreateEightBitBatch(
            topology.SourceUniverseVersion,
            sourceIds: [0, 2]);

        Assert.Throws<ArgumentOutOfRangeException>(() => router.Route(batch));
    }

    [Fact]
    public void RouteRejectsInvalidLookupIds()
    {
        var topology = CreateTopology(sourceCount: 2, partitionCount: 1, shardCount: 1);
        var router = new RadarProcessingBatchRouter(topology);
        var route = router.Route(CreateEightBitBatch(topology.SourceUniverseVersion, sourceIds: [0]));

        Assert.Throws<ArgumentOutOfRangeException>(() => route.GetRoutedEvent(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => route.GetRoutedEvent(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => route.GetPartition(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => route.GetPartition(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => route.GetShard(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => route.GetShard(1));
    }
}
