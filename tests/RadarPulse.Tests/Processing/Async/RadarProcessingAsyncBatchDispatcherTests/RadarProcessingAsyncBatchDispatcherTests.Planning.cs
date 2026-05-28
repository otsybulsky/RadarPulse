using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncBatchDispatcherTests
{
    [Fact]
    public void DispatcherCreatesOneShardWorkItemPlanAgainstCapturedTopology()
    {
        using var group = CreateStartedGroup(workerCount: 2, queueCapacity: 2);
        var topology = CreateTopology(sourceCount: 6, partitionCount: 6, shardCount: 3);
        var providerCallCount = 0;
        var dispatcher = new RadarProcessingAsyncBatchDispatcher(
            group,
            () =>
            {
                providerCallCount++;
                return topology;
            });
        var batch = CreateEightBitBatch(topology.SourceUniverseVersion, sourceIds: [0, 1, 3, 5]);

        var plan = dispatcher.CreatePlan(batchSequence: 17, batch);

        Assert.Equal(1, providerCallCount);
        Assert.Equal(17, plan.BatchSequence);
        Assert.Equal(topology.Version, plan.TopologyVersion);
        Assert.Equal(topology.ShardCount, plan.ExpectedWorkItemCount);
        Assert.Equal(topology.PartitionCount, plan.PartitionCount);
        Assert.Equal(topology.ShardCount, plan.ShardCount);
        Assert.Equal(batch.EventCount, plan.RoutedEventCount);
        Assert.Equal(topology.ShardCount, plan.WorkItems.Count);

        for (var shardId = 0; shardId < topology.ShardCount; shardId++)
        {
            var workItem = plan.WorkItems[shardId];
            Assert.Equal(shardId, workItem.WorkItemId);
            Assert.Equal(shardId, workItem.ShardId);
            Assert.Equal(shardId % group.Options.WorkerCount, workItem.WorkerId.Value);
            Assert.Equal(topology.Version, workItem.TopologyVersion);
            Assert.Equal(
                topology.Partitions
                    .Where(partition => partition.ShardId == shardId)
                    .Select(partition => partition.PartitionId)
                    .ToArray(),
                workItem.PartitionIds);
        }
    }
}
