using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingCorePartitionedBarrierTests
{
    [Fact]
    public void PartitionedBarrierMatchesSequentialMetricsAndSnapshots()
    {
        var universe = CreateUniverse(sourceCount: 6);
        var sequential = new RadarProcessingCore(universe);
        var partitioned = CreatePartitionedCore(universe, partitionCount: 6, shardCount: 3);
        var batch = CreateMixedBatch();

        var sequentialResult = sequential.Process(batch);
        var partitionedResult = partitioned.Process(batch);

        Assert.True(sequentialResult.IsValid);
        Assert.True(partitionedResult.IsValid);
        Assert.Equal(RadarProcessingExecutionMode.PartitionedBarrier, partitionedResult.ExecutionMode);
        Assert.Equal(sequentialResult.Metrics, partitionedResult.Metrics);
        Assert.Equal(sequential.CreateSourceSnapshots(), partitioned.CreateSourceSnapshots());
    }

    [Fact]
    public void PartitionedBarrierPreservesOwnedAndLeasedParity()
    {
        var universe = CreateUniverse(sourceCount: 6);
        var ownedCore = CreatePartitionedCore(universe, partitionCount: 6, shardCount: 3);
        var leasedCore = CreatePartitionedCore(universe, partitionCount: 6, shardCount: 3);
        var ownedBatch = CreateMixedBatchBuilder().Build();

        var ownedResult = ownedCore.Process(ownedBatch);
        RadarProcessingResult? leasedResult = null;
        CreateMixedBatchBuilder().ConsumeLeased(batch =>
        {
            Assert.Equal(RadarEventBatchLifetime.Leased, batch.Lifetime);
            leasedResult = leasedCore.Process(batch);
        });

        Assert.NotNull(leasedResult);
        Assert.Equal(ownedResult.Metrics, leasedResult.Metrics);
        Assert.Equal(ownedCore.CreateSourceSnapshots(), leasedCore.CreateSourceSnapshots());
    }
}
