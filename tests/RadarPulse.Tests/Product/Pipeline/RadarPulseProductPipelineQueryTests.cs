using RadarPulse.Application.Product;
using RadarPulse.Infrastructure.Product;

namespace RadarPulse.Tests.Product;

public sealed class RadarPulseProductPipelineQueryTests
{
    [Fact]
    public async Task LatestRunAndRunListFollowPublicationOrder()
    {
        var service = new RadarPulseProductPipelineService();
        await service.RunSyntheticAsync(
            new RadarPulseProductPipelineSyntheticRunRequest("product-query-first"));
        await service.RunSyntheticAsync(
            new RadarPulseProductPipelineSyntheticRunRequest("product-query-second"));

        var runs = service.ListRuns();
        var latest = service.TryGetLatestRun();

        Assert.Equal(2, runs.Count);
        Assert.Equal("product-query-first", runs[0].RunId);
        Assert.Equal("product-query-second", runs[1].RunId);
        Assert.True(latest.Found);
        Assert.Equal("product-query-second", latest.Value!.RunId);
    }

    [Fact]
    public async Task BatchAndSourceQueriesReturnProductRecords()
    {
        var service = new RadarPulseProductPipelineService();
        await service.RunSyntheticAsync(
            new RadarPulseProductPipelineSyntheticRunRequest(
                "product-query-detail",
                SourceCount: 2,
                BatchCount: 2,
                EventsPerBatch: 2));

        var batches = service.ListBatches("product-query-detail");
        var batch = service.TryGetBatch("product-query-detail", providerSequence: 1);
        var sources = service.ListSources("product-query-detail");
        var source = service.TryGetSource("product-query-detail", sourceId: 1);

        Assert.True(batches.Found);
        Assert.Equal(2, batches.Value!.Count);
        Assert.True(batch.Found);
        Assert.Equal(1, batch.Value!.ProviderSequence);
        Assert.True(sources.Found);
        Assert.Equal(2, sources.Value!.Count);
        Assert.True(source.Found);
        Assert.Equal(1, source.Value!.Identity.SourceId);
    }

    [Fact]
    public async Task HandlerOutputDiagnosticsAndCapacityRemainQueryable()
    {
        var service = new RadarPulseProductPipelineService();
        await service.RunSyntheticAsync(
            new RadarPulseProductPipelineSyntheticRunRequest(
                "product-query-handler",
                HandlerSet: RadarPulseProductHandlerSet.CounterChecksum));

        var handler = service.TryGetHandlerOutput(
            "product-query-handler",
            sourceId: 0,
            fieldName: "benchmark.events");
        var diagnostics = service.TryGetDiagnostics("product-query-handler");
        var capacity = service.TryGetCapacityEvidence("product-query-handler");

        Assert.True(handler.Found);
        Assert.Equal("benchmark.events", handler.Value!.Name);
        Assert.True(diagnostics.Found);
        Assert.True(diagnostics.Value!.ProcessingCompletenessPassed);
        Assert.True(capacity.Found);
        Assert.True(capacity.Value!.IsReady);
        Assert.Equal(2, capacity.Value.AcceptedBatchCount);
    }

    [Fact]
    public async Task QueryMissesReturnNotFoundResponses()
    {
        var service = new RadarPulseProductPipelineService();
        await service.RunSyntheticAsync(
            new RadarPulseProductPipelineSyntheticRunRequest("product-query-miss"));

        Assert.False(service.TryGetRun("missing-run").Found);
        Assert.False(service.TryGetBatch("product-query-miss", providerSequence: 99).Found);
        Assert.False(service.TryGetSource("product-query-miss", sourceId: 99).Found);
        Assert.False(service.TryGetHandlerOutput(
            "product-query-miss",
            sourceId: 0,
            fieldName: "missing.field").Found);
        Assert.False(service.TryGetDiagnostics("missing-run").Found);
        Assert.False(service.TryGetCapacityEvidence("missing-run").Found);
    }
}
