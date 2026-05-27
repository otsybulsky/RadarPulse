using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingProductionPipelineSummaryTests
{
    [Fact]
    public void ReadySummaryReportsAcceptedDefaults()
    {
        var configuration = RadarProcessingProductionPipelineProfile.Resolve();

        var summary = RadarProcessingProductionPipelineOperatorSummary.Create(configuration);

        Assert.True(summary.IsReady);
        Assert.Equal(RadarProcessingProductionPipelineRunState.Completed, summary.RunState);
        Assert.Equal(RadarProcessingProductionPipelineFallbackRecommendation.None, summary.FallbackRecommendation);
        Assert.False(summary.HasBlockingReason);
        Assert.Equal(RadarProcessingProductionPipelineHandlerMode.Auto, summary.HandlerMode);
        Assert.True(summary.ProcessingComplete);
        Assert.True(summary.ReleaseHealthy);
        Assert.Equal(RadarProcessingArchiveProviderMode.QueuedOwned, summary.Configuration.ProviderMode.Value);
    }

    [Fact]
    public void InvalidConfigurationBlocksReadiness()
    {
        var configuration = RadarProcessingProductionPipelineProfile.Resolve(
            new RadarProcessingProductionPipelineOptions(workerCount: 0));

        var summary = RadarProcessingProductionPipelineOperatorSummary.Create(configuration);

        Assert.False(summary.IsReady);
        Assert.Equal(
            RadarProcessingProductionPipelineFallbackRecommendation.FixConfiguration,
            summary.FallbackRecommendation);
        Assert.Contains(nameof(RadarProcessingProductionPipelineOptions.WorkerCount), summary.FirstBlockingReason);
    }

    [Fact]
    public void FailedDurableEnvelopeBlocksReadinessWithRecoveryRecommendation()
    {
        var configuration = RadarProcessingProductionPipelineProfile.Resolve();
        var readiness = CreateReadiness(
            RadarProcessingDurableEnvelopeState.Failed,
            "failed durable envelope");

        var summary = RadarProcessingProductionPipelineOperatorSummary.Create(
            configuration,
            durableReadiness: readiness);

        Assert.False(summary.IsReady);
        Assert.Equal(
            RadarProcessingProductionPipelineFallbackRecommendation.RetryOrPoisonEnvelope,
            summary.FallbackRecommendation);
        Assert.Equal(RadarProcessingDurableEnvelopeState.Failed, summary.FirstBlockingState);
        Assert.Equal("failed durable envelope", summary.FirstBlockingReason);
    }

    [Fact]
    public void ClaimedDurableEnvelopeRecommendsExplicitClaimRecovery()
    {
        var configuration = RadarProcessingProductionPipelineProfile.Resolve();
        var readiness = CreateReadiness(
            RadarProcessingDurableEnvelopeState.Claimed,
            "claimed durable envelope");

        var summary = RadarProcessingProductionPipelineOperatorSummary.Create(
            configuration,
            durableReadiness: readiness);

        Assert.False(summary.IsReady);
        Assert.Equal(
            RadarProcessingProductionPipelineFallbackRecommendation.RecoverClaimedEnvelope,
            summary.FallbackRecommendation);
        Assert.Equal("claimed durable envelope", summary.FirstBlockingReason);
    }

    [Fact]
    public void PoisonDurableEnvelopeRecommendsQuarantineAction()
    {
        var configuration = RadarProcessingProductionPipelineProfile.Resolve();
        var readiness = CreateReadiness(
            RadarProcessingDurableEnvelopeState.Poison,
            "poison durable envelope");

        var summary = RadarProcessingProductionPipelineOperatorSummary.Create(
            configuration,
            durableReadiness: readiness);

        Assert.False(summary.IsReady);
        Assert.Equal(
            RadarProcessingProductionPipelineFallbackRecommendation.QuarantinePoisonEnvelope,
            summary.FallbackRecommendation);
        Assert.Equal("poison durable envelope", summary.FirstBlockingReason);
    }

    [Fact]
    public void CurrentRetainedPressureBlocksReadiness()
    {
        var configuration = RadarProcessingProductionPipelineProfile.Resolve();
        var retainedPressure = new RadarProcessingRetainedResourcePressureSummary(
            currentPendingRetainedBatchCount: 1,
            currentPendingRetainedPayloadBytes: 128,
            pendingRetainedBatchCountHighWatermark: 1,
            pendingRetainedPayloadBytesHighWatermark: 128,
            combinedRetainedBatchCountHighWatermark: 1,
            combinedRetainedPayloadBytesHighWatermark: 128);

        var summary = RadarProcessingProductionPipelineOperatorSummary.Create(
            configuration,
            retainedPressure: retainedPressure);

        Assert.False(summary.IsReady);
        Assert.False(summary.ReleaseHealthy);
        Assert.Equal(1, summary.CurrentRetainedBatchCount);
        Assert.Equal(
            RadarProcessingProductionPipelineFallbackRecommendation.ReleaseRetainedResources,
            summary.FallbackRecommendation);
        Assert.Contains("retained pressure", summary.FirstBlockingReason);
    }

    [Fact]
    public void IncompatibleDurableAdapterBlocksReadiness()
    {
        var configuration = RadarProcessingProductionPipelineProfile.Resolve();
        var adapter = new RadarProcessingDurableAdapterSummary(
            "file",
            schemaVersion: 1,
            storageIdentity: "store",
            RadarProcessingDurableAdapterCompatibilityStatus.Incompatible,
            "unsupported schema");

        var summary = RadarProcessingProductionPipelineOperatorSummary.Create(
            configuration,
            durableAdapter: adapter);

        Assert.False(summary.IsReady);
        Assert.Equal(
            RadarProcessingProductionPipelineFallbackRecommendation.InspectDurableAdapter,
            summary.FallbackRecommendation);
        Assert.Equal("unsupported schema", summary.FirstBlockingReason);
    }

    private static RadarProcessingDurableRuntimeReadinessSummary CreateReadiness(
        RadarProcessingDurableEnvelopeState state,
        string reason)
    {
        var queueSummary = new RadarProcessingDurableQueueSummary(
            acceptedEnvelopeCount: 1,
            firstBlockingBatchId: new RadarProcessingDurableBatchId("batch-0"),
            firstBlockingSequence: RadarProcessingQueuedBatchSequence.Initial,
            firstBlockingState: state,
            firstBlockingReason: reason);

        return new RadarProcessingDurableRuntimeReadinessSummary(queueSummary);
    }
}
