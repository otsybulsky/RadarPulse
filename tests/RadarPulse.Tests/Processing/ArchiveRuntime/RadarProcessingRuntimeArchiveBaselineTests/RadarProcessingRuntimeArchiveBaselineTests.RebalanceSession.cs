using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRuntimeArchiveBaselineTests
{
    [Fact]
    public void BaselineCanCreateRebalanceSessionForSuppliedUniverse()
    {
        var universe = CreateUniverse(sourceCount: 8);

        var session = RadarProcessingRuntimeArchiveBaseline.CreateRebalanceSession(
            universe,
            partitionCount: 8,
            shardCount: 4);

        Assert.True(RadarProcessingRuntimeArchiveBaseline.MatchesCoreOptions(session.Core.Options));
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, session.Core.Options.ExecutionMode);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount,
            session.Core.Options.AsyncExecution.WorkerCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity,
            session.Core.Options.AsyncExecution.QueueCapacity);
        Assert.Equal(8, session.PolicyState.PartitionCount);
        Assert.Equal(4, session.PolicyState.ShardCount);
    }

    [Fact]
    public async Task BaselineRebalanceSessionComposesWithOmittedQueuedOverlapDefault()
    {
        var universe = CreateUniverse(sourceCount: 8);
        var session = RadarProcessingRuntimeArchiveBaseline.CreateRebalanceSession(
            universe,
            partitionCount: 8,
            shardCount: 4);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();

        var result = await runner.RunRebalanceAsync(
            (publisher, cancellationToken) =>
            {
                publisher.Publish(
                    CreateEightBitBatch(universe.Version, [0, 1, 2, 3]),
                    cancellationToken);
                return CreatePublishResult(
                    universe,
                    batchCount: 1,
                    eventCount: 4,
                    payloadBytes: 4);
            },
            session);

        Assert.True(result.IsCompleted);
        Assert.True(result.HasRetainedPayloadPrewarm);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.OverlapTelemetry.RetentionStrategy);
        Assert.Equal(0, result.ProviderResult.RetentionTelemetry.ReleaseFailedCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);

        var processing = Assert.Single(result.Consumer.SessionResult.ProcessingResults);
        Assert.True(processing.IsSuccessful);
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, processing.ProcessingResult?.ExecutionMode);
        Assert.Equal(8, processing.ProcessingResult?.PartitionCount);
        Assert.Equal(4, processing.ProcessingResult?.ShardCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount,
            processing.RebalanceResult?.WorkerTelemetry?.WorkerCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity,
            processing.RebalanceResult?.WorkerTelemetry?.QueueCapacity);
        Assert.True(processing.RebalanceResult?.Validation.IsValid);
    }

    [Fact]
    public async Task SuppliedRebalanceSessionKeepsExplicitExecutionMode()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount: 4,
                shardCount: 2));
        var session = new RadarProcessingRebalanceSession(core);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var options = new RadarProcessingArchiveQueuedOverlapOptions(
            new RadarProcessingProviderQueueOptions(capacity: 2, recentDetailCapacity: 16));

        var result = await runner.RunRebalanceAsync(
            (publisher, cancellationToken) =>
            {
                publisher.Publish(
                    CreateEightBitBatch(universe.Version, [0, 1]),
                    cancellationToken);
                return CreatePublishResult(
                    universe,
                    batchCount: 1,
                    eventCount: 2,
                    payloadBytes: 2);
            },
            session,
            options);

        Assert.True(result.IsCompleted);
        var processing = Assert.Single(result.Consumer.SessionResult.ProcessingResults);
        Assert.True(processing.IsSuccessful);
        Assert.Equal(RadarProcessingExecutionMode.PartitionedBarrier, processing.ProcessingResult?.ExecutionMode);
        Assert.Null(processing.RebalanceResult?.WorkerTelemetry);
        Assert.False(RadarProcessingRuntimeArchiveBaseline.MatchesCoreOptions(session.Core.Options));
    }
}
