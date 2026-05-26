using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingProductionPipelineConfigurationTests
{
    [Fact]
    public void DefaultProfileResolvesAcceptedRuntimeDefaults()
    {
        var configuration = RadarProcessingProductionPipelineProfile.Resolve();

        Assert.True(configuration.IsValid);
        Assert.Equal("production-pipeline", configuration.ProfileName);
        Assert.Equal(RadarProcessingArchiveProviderMode.QueuedOwned, configuration.ProviderMode.Value);
        Assert.Equal(RadarProcessingProductionPipelineOptionSource.Profile, configuration.ProviderMode.Source);
        Assert.Equal(
            RadarProcessingQueuedProviderOverlapMode.ProducerConsumer,
            configuration.ProviderOverlapMode.Value);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, configuration.RetentionStrategy.Value);
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, configuration.ExecutionMode.Value);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount, configuration.WorkerCount.Value);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity,
            configuration.WorkerQueueCapacity.Value);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity,
            configuration.ProviderQueueCapacity.Value);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes,
            configuration.RetainedPayloadBytes.Value);
        Assert.Equal(
            RadarProcessingRuntimeArchiveBaseline.OrderedActiveBatchCapacity,
            configuration.OrderedActiveBatchCapacity.Value);
        Assert.Equal(
            RadarProcessingProductionPipelineDurableAdapterKind.File,
            configuration.DurableAdapterKind.Value);
        Assert.Equal(RadarProcessingProductionPipelineHandlerMode.Auto, configuration.HandlerMode.Value);
        Assert.Null(configuration.WorkloadBatchLimit.Value);
        Assert.Null(configuration.FirstInvalidOption);
        Assert.False(configuration.HasWarnings);

        Assert.True(RadarProcessingRuntimeArchiveBaseline.MatchesQueuedOverlapOptions(
            configuration.CreateQueuedOverlapOptions()));
        Assert.True(RadarProcessingRuntimeArchiveBaseline.MatchesOrderedConcurrencyOptions(
            configuration.CreateOrderedConcurrencyOptions()));
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount,
            configuration.CreateAsyncExecution().WorkerCount);
    }

    [Fact]
    public void ExplicitOverridesArePreservedWithOverrideProvenance()
    {
        var configuration = RadarProcessingProductionPipelineProfile.Resolve(
            new RadarProcessingProductionPipelineOptions(
                profileName: "local-capacity",
                workerCount: 2,
                workerQueueCapacity: 3,
                providerQueueCapacity: 4,
                retainedPayloadBytes: 1024,
                orderedActiveBatchCapacity: 2,
                workloadBatchLimit: 8,
                handlerMode: RadarProcessingProductionPipelineHandlerMode.HandlerFree));

        Assert.True(configuration.IsValid);
        Assert.Equal("local-capacity", configuration.ProfileName);
        Assert.Equal(2, configuration.WorkerCount.Value);
        Assert.Equal(RadarProcessingProductionPipelineOptionSource.ExplicitOverride, configuration.WorkerCount.Source);
        Assert.Equal(3, configuration.WorkerQueueCapacity.Value);
        Assert.Equal(4, configuration.ProviderQueueCapacity.Value);
        Assert.Equal(1024, configuration.RetainedPayloadBytes.Value);
        Assert.Equal(2, configuration.OrderedActiveBatchCapacity.Value);
        Assert.Equal(8, configuration.WorkloadBatchLimit.Value);
        Assert.Equal(
            RadarProcessingProductionPipelineOptionSource.ExplicitOverride,
            configuration.WorkloadBatchLimit.Source);
        Assert.Equal(RadarProcessingProductionPipelineHandlerMode.HandlerFree, configuration.HandlerMode.Value);
        Assert.True(configuration.HasWarnings);
    }

    [Fact]
    public void InvalidCapacityFailsClosedWithFirstInvalidOption()
    {
        var configuration = RadarProcessingProductionPipelineProfile.Resolve(
            new RadarProcessingProductionPipelineOptions(workerCount: 0, providerQueueCapacity: -1));

        Assert.False(configuration.IsValid);
        Assert.Equal(nameof(RadarProcessingProductionPipelineOptions.WorkerCount), configuration.FirstInvalidOption);
        Assert.Contains("positive", configuration.FirstInvalidReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnsupportedDurableAdapterKindFailsClosed()
    {
        var configuration = RadarProcessingProductionPipelineProfile.Resolve(
            new RadarProcessingProductionPipelineOptions(
                durableAdapterKind: (RadarProcessingProductionPipelineDurableAdapterKind)99));

        Assert.False(configuration.IsValid);
        Assert.Equal(
            nameof(RadarProcessingProductionPipelineOptions.DurableAdapterKind),
            configuration.FirstInvalidOption);
    }

    [Fact]
    public void SilentBorrowedProviderFallbackIsRejected()
    {
        var configuration = RadarProcessingProductionPipelineProfile.Resolve(
            new RadarProcessingProductionPipelineOptions(silentBorrowedProviderFallback: true));

        Assert.False(configuration.IsValid);
        Assert.Equal(
            nameof(RadarProcessingProductionPipelineOptions.SilentBorrowedProviderFallback),
            configuration.FirstInvalidOption);
        Assert.Contains("fallback", configuration.FirstInvalidReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BorrowedProviderModeIsRejectedByProductionProfile()
    {
        var configuration = RadarProcessingProductionPipelineProfile.Resolve(
            new RadarProcessingProductionPipelineOptions(
                providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed));

        Assert.False(configuration.IsValid);
        Assert.Equal(nameof(RadarProcessingProductionPipelineOptions.ProviderMode), configuration.FirstInvalidOption);
    }
}
