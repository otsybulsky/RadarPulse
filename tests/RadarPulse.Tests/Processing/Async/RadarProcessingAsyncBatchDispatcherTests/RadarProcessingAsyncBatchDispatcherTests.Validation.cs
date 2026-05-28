using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncBatchDispatcherTests
{
    [Fact]
    public void DispatcherRejectsRouteTopologyMismatch()
    {
        using var group = CreateStartedGroup(workerCount: 3, queueCapacity: 1);
        var manager = CreateTopologyManager(sourceCount: 6, partitionCount: 6, shardCount: 3);
        var dispatcher = new RadarProcessingAsyncBatchDispatcher(group, () => manager.Current);
        var batch = CreateEightBitBatch(manager.Current.SourceUniverseVersion, sourceIds: [1]);
        var firstTopology = manager.Current;
        var firstRoute = new RadarProcessingBatchRouter(firstTopology).Route(batch);

        var move = manager.MovePartition(
            new RadarProcessingTopologyMoveRequest(
                firstTopology.Version,
                partitionId: 1,
                sourceShardId: 0,
                targetShardId: 2));

        Assert.True(move.Succeeded);
        Assert.Throws<ArgumentException>(() =>
            dispatcher.CreatePlan(batchSequence: 1, manager.Current, firstRoute));
    }

    [Fact]
    public void DispatchContractsRejectInvalidShapes()
    {
        using var group = CreateStartedGroup(workerCount: 1, queueCapacity: 1);
        var topology = CreateTopology(sourceCount: 1, partitionCount: 1, shardCount: 1);
        var dispatcher = new RadarProcessingAsyncBatchDispatcher(group, () => topology);
        var batch = CreateEightBitBatch(topology.SourceUniverseVersion, sourceIds: [0]);
        var plan = dispatcher.CreatePlan(batchSequence: 1, batch);
        var workerResult = RadarProcessingAsyncWorkerGroupResult.Completed(
            new RadarProcessingWorkerGroupStatus(
                RadarProcessingWorkerGroupState.Running,
                RadarProcessingWorkerHealth.Healthy,
                workerCount: 1,
                queueCapacity: 1),
            plan.Scope.RecordCompletion(RadarProcessingAsyncWorkCompletion.Succeeded(plan.WorkItems[0])),
            new RadarProcessingAsyncWorkerGroupDrainResult());

        Assert.Throws<ArgumentNullException>(() => new RadarProcessingAsyncBatchDispatcher(null!, () => topology));
        Assert.Throws<ArgumentNullException>(() => new RadarProcessingAsyncBatchDispatcher(group, null!));
        Assert.Throws<ArgumentNullException>(() => dispatcher.CreatePlan(1, null!));
        Assert.Throws<ArgumentNullException>(() => dispatcher.CreatePlan(1, null!, plan.Route));
        Assert.Throws<ArgumentNullException>(() => dispatcher.CreatePlan(1, topology, null!));
        Assert.Throws<ArgumentNullException>(() => new RadarProcessingAsyncDispatchPlan(null!, plan.Route, plan.WorkItems));
        Assert.Throws<ArgumentNullException>(() => new RadarProcessingAsyncDispatchPlan(plan.Scope, null!, plan.WorkItems));
        Assert.Throws<ArgumentNullException>(() => new RadarProcessingAsyncDispatchPlan(plan.Scope, plan.Route, null!));
        Assert.Throws<ArgumentNullException>(() => new RadarProcessingAsyncDispatchResult(null!, workerResult));
        Assert.Throws<ArgumentNullException>(() => new RadarProcessingAsyncDispatchResult(plan, null!));
    }
}
