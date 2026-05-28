using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingCoreSequentialTests
{
    [Fact]
    public void ConstructorRejectsInvalidInputsAndInvalidTopology()
    {
        var universe = CreateUniverse(sourceCount: 2);

        Assert.Throws<ArgumentNullException>(() => new RadarProcessingCore(null!));
        Assert.Throws<ArgumentException>(() => new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.Sequential,
                partitionCount: 3,
                shardCount: 1)));
    }

    [Fact]
    public void ConstructorAcceptsPartitionedBarrierMode()
    {
        var universe = CreateUniverse(sourceCount: 4);

        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount: 4,
                shardCount: 2));

        Assert.Equal(RadarProcessingExecutionMode.PartitionedBarrier, core.Options.ExecutionMode);
        Assert.Equal(4, core.Topology.PartitionCount);
        Assert.Equal(2, core.Topology.ShardCount);
    }

    [Fact]
    public void ProcessRejectsNullBatch()
    {
        var core = new RadarProcessingCore(CreateUniverse(sourceCount: 1));

        Assert.Throws<ArgumentNullException>(() => core.Process(null!));
    }
}
