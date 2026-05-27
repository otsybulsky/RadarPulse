using RadarPulse.Application.Product;
using RadarPulse.Infrastructure.Product;

namespace RadarPulse.Tests.Product;

public sealed class RadarPulseProductPipelineServiceTests
{
    [Fact]
    public async Task SyntheticProductRunCompletesAndStoresDetail()
    {
        var service = new RadarPulseProductPipelineService();

        var detail = await service.RunSyntheticAsync(
            new RadarPulseProductPipelineSyntheticRunRequest(
                "product-service-run",
                SourceCount: 2,
                BatchCount: 2,
                EventsPerBatch: 2));

        Assert.True(detail.IsReady);
        Assert.True(detail.HasReadModel);
        Assert.Equal(RadarPulseProductRunState.Completed, detail.Summary.State);
        Assert.Equal(RadarPulseProductInputKind.Synthetic, detail.Summary.Input.Kind);
        Assert.Equal(2, detail.Summary.BatchCount);
        Assert.Equal(2, detail.Summary.SourceCount);
        Assert.Equal(1, service.Count);
        Assert.True(service.TryGetLatestRun().Found);
        Assert.Equal("product-service-run", service.TryGetLatestRun().Value!.RunId);
    }

    [Fact]
    public async Task SyntheticProductRunReportsMergeableHandlerPosture()
    {
        var service = new RadarPulseProductPipelineService();

        var detail = await service.RunSyntheticAsync(
            new RadarPulseProductPipelineSyntheticRunRequest(
                "product-mergeable",
                HandlerSet: RadarPulseProductHandlerSet.CounterChecksum));

        Assert.True(detail.IsReady);
        Assert.Equal(RadarPulseProductHandlerMode.MergeableDelta, detail.Summary.HandlerMode);
        Assert.True(detail.Diagnostics!.UsesOrderedHandlerDeltaMerge);
        Assert.NotEmpty(detail.Sources[0].HandlerValues);
    }

    [Fact]
    public async Task SyntheticProductRunReportsSnapshotSequentialFallback()
    {
        var service = new RadarPulseProductPipelineService();

        var detail = await service.RunSyntheticAsync(
            new RadarPulseProductPipelineSyntheticRunRequest(
                "product-snapshot",
                HandlerSet: RadarPulseProductHandlerSet.SnapshotCounting));

        Assert.True(detail.IsReady);
        Assert.Equal(RadarPulseProductHandlerMode.SnapshotSequential, detail.Summary.HandlerMode);
        Assert.True(detail.Diagnostics!.UsesSequentialHandlerFallback);
        Assert.NotEmpty(detail.OperatorSummary.Warnings);
    }

    [Fact]
    public async Task UnsupportedProductHandlerBlocksWithoutReadModelPublication()
    {
        var service = new RadarPulseProductPipelineService();

        var detail = await service.RunSyntheticAsync(
            new RadarPulseProductPipelineSyntheticRunRequest(
                "product-unsupported",
                HandlerSet: RadarPulseProductHandlerSet.Unsupported));

        Assert.False(detail.IsReady);
        Assert.False(detail.HasReadModel);
        Assert.Equal(RadarPulseProductRunState.Blocked, detail.Summary.State);
        Assert.Equal(
            RadarPulseProductFallbackRecommendation.ResolveHandlerPosture,
            detail.Summary.FallbackRecommendation);
        Assert.Contains("product-unsupported", detail.Summary.FirstBlockingReason);
        Assert.Equal(1, service.Count);
    }

    [Fact]
    public async Task InvalidSyntheticRequestFailsBeforePublishingProductHistory()
    {
        var service = new RadarPulseProductPipelineService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await service.RunSyntheticAsync(
                new RadarPulseProductPipelineSyntheticRunRequest(
                    "product-invalid",
                    SourceCount: 0)));

        Assert.Equal(0, service.Count);
    }

    [Fact]
    public async Task MissingArchiveFileRequestFailsBeforePublishingProductHistory()
    {
        var service = new RadarPulseProductPipelineService();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await service.RunArchiveFileAsync(
                new RadarPulseProductPipelineArchiveFileRunRequest(
                    "product-missing-archive",
                    path)));

        Assert.Equal(0, service.Count);
    }
}
