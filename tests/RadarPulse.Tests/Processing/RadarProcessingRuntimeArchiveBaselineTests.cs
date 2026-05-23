using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingRuntimeArchiveBaselineTests
{
    [Fact]
    public void BaselineCreatesRolloutAsyncCoreOptions()
    {
        var options = RadarProcessingRuntimeArchiveBaseline.CreateCoreOptions(
            partitionCount: 8,
            shardCount: 4);

        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, options.ExecutionMode);
        Assert.Equal(8, options.PartitionCount);
        Assert.Equal(4, options.ShardCount);
        Assert.True(options.EnableValidation);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount, options.AsyncExecution.WorkerCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity,
            options.AsyncExecution.QueueCapacity);
        Assert.True(RadarProcessingRuntimeArchiveBaseline.MatchesCoreOptions(options));
    }

    [Fact]
    public void BaselineCreatesRolloutAsyncExecutionOptions()
    {
        var asyncExecution = RadarProcessingRuntimeArchiveBaseline.CreateAsyncExecution();

        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount, asyncExecution.WorkerCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity,
            asyncExecution.QueueCapacity);
    }

    [Fact]
    public void BaselineKeepsQueuedOverlapProviderDefaultSeparate()
    {
        var options = RadarProcessingRuntimeArchiveBaseline.QueuedOverlapOptions;

        Assert.Same(RadarProcessingArchiveQueuedOverlapOptions.Default, options);
        Assert.True(RadarProcessingRuntimeArchiveBaseline.MatchesQueuedOverlapOptions(options));
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity,
            options.QueueOptions.Capacity);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes,
            options.QueueOptions.MaxRetainedPayloadBytes);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy,
            options.RetainedPayloadOptions.Strategy);
        Assert.Equal(
            RadarProcessingRetainedPayloadPrewarmOptions.RolloutDefault,
            options.RetainedPayloadPrewarmOptions);
    }

    [Fact]
    public void ExplicitDiagnosticQueuedOverlapOptionsRemainOutsideBaseline()
    {
        var explicitOptions = new RadarProcessingArchiveQueuedOverlapOptions();

        Assert.False(RadarProcessingRuntimeArchiveBaseline.MatchesQueuedOverlapOptions(explicitOptions));
        Assert.Equal(RadarProcessingProviderQueueOptions.Default, explicitOptions.QueueOptions);
        Assert.Equal(RadarProcessingRetainedPayloadOptions.Default, explicitOptions.RetainedPayloadOptions);
        Assert.Equal(RadarProcessingRetainedPayloadPrewarmOptions.None, explicitOptions.RetainedPayloadPrewarmOptions);
    }

    [Fact]
    public void BaselineMatchRejectsNonRolloutExecutionShapes()
    {
        var sequential = new RadarProcessingCoreOptions(
            RadarProcessingExecutionMode.Sequential,
            partitionCount: 8,
            shardCount: 4);
        var wrongWorkerCount = new RadarProcessingCoreOptions(
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 8,
            shardCount: 4,
            asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 3, queueCapacity: 8));
        var wrongQueueCapacity = new RadarProcessingCoreOptions(
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 8,
            shardCount: 4,
            asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 4, queueCapacity: 7));

        Assert.False(RadarProcessingRuntimeArchiveBaseline.MatchesCoreOptions(sequential));
        Assert.False(RadarProcessingRuntimeArchiveBaseline.MatchesCoreOptions(wrongWorkerCount));
        Assert.False(RadarProcessingRuntimeArchiveBaseline.MatchesCoreOptions(wrongQueueCapacity));
    }

    [Fact]
    public void BaselineCanCreateCoreForSuppliedUniverseWithoutChangingCoreDefault()
    {
        var universe = new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: 8,
            rangeBandCount: 1);

        var core = RadarProcessingRuntimeArchiveBaseline.CreateCore(
            universe,
            partitionCount: 8,
            shardCount: 4);

        Assert.True(RadarProcessingRuntimeArchiveBaseline.MatchesCoreOptions(core.Options));
        Assert.Equal(RadarProcessingExecutionMode.Sequential, RadarProcessingCoreOptions.Default.ExecutionMode);
    }
}
