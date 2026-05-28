using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRuntimeArchiveBaselineTests
{
    [Fact]
    public void BaselineExposesOrderedConcurrencyCapacityIndependentFromQueues()
    {
        var ordered = RadarProcessingRuntimeArchiveBaseline.OrderedConcurrencyOptions;
        var provider = RadarProcessingRuntimeArchiveBaseline.QueuedOverlapOptions;
        var asyncExecution = RadarProcessingRuntimeArchiveBaseline.CreateAsyncExecution();

        Assert.Equal(
            RadarProcessingOrderedConcurrencyOptions.DefaultActiveBatchCapacity,
            ordered.ActiveBatchCapacity);
        Assert.Equal(
            RadarProcessingRuntimeArchiveBaseline.OrderedActiveBatchCapacity,
            ordered.ActiveBatchCapacity);
        Assert.True(RadarProcessingRuntimeArchiveBaseline.MatchesOrderedConcurrencyOptions(ordered));
        Assert.NotEqual(provider.QueueOptions.Capacity, ordered.ActiveBatchCapacity);
        Assert.NotEqual(asyncExecution.QueueCapacity, ordered.ActiveBatchCapacity);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity, provider.QueueOptions.Capacity);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity, asyncExecution.QueueCapacity);
    }

    [Fact]
    public void OrderedConcurrencyOptionsValidateActiveBatchCapacity()
    {
        var sequential = RadarProcessingOrderedConcurrencyOptions.Sequential;
        var explicitConcurrent = new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 2);

        Assert.Equal(1, sequential.ActiveBatchCapacity);
        Assert.True(sequential.IsSequential);
        Assert.Equal(2, explicitConcurrent.ActiveBatchCapacity);
        Assert.False(explicitConcurrent.IsSequential);
        Assert.False(RadarProcessingRuntimeArchiveBaseline.MatchesOrderedConcurrencyOptions(sequential));
        Assert.False(RadarProcessingRuntimeArchiveBaseline.MatchesOrderedConcurrencyOptions(explicitConcurrent));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarProcessingOrderedConcurrencyOptions(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarProcessingOrderedConcurrencyOptions(-1));
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
}
