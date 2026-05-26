using RadarPulse.Application.Product;
using RadarPulse.Infrastructure.Product;

namespace RadarPulse.Tests.Product;

public sealed class RadarPulseProductRunHistoryStoreTests
{
    [Fact]
    public async Task DefaultProductServiceUsesHealthyInMemoryHistory()
    {
        var service = new RadarPulseProductPipelineService();

        await service.RunSyntheticAsync(
            new RadarPulseProductPipelineSyntheticRunRequest("history-default"));

        var readiness = service.HistoryReadiness;

        Assert.True(readiness.IsReady);
        Assert.Equal(RadarPulseProductRunHistoryStorageKind.InMemory, readiness.StorageKind);
        Assert.Equal("in-memory", readiness.StorageIdentity);
        Assert.Equal(1, readiness.LoadedRunCount);
        Assert.Equal(0, readiness.RejectedRunCount);
        Assert.Empty(readiness.FirstBlockingReason);
    }

    [Fact]
    public async Task InjectedInMemoryHistoryPreservesPublicationOrder()
    {
        var history = new RadarPulseProductInMemoryRunHistoryStore();
        var service = new RadarPulseProductPipelineService(historyStore: history);

        await service.RunSyntheticAsync(
            new RadarPulseProductPipelineSyntheticRunRequest("history-first"));
        await service.RunSyntheticAsync(
            new RadarPulseProductPipelineSyntheticRunRequest("history-second"));

        var runs = history.ListRuns();
        var latest = history.TryGetLatestRun();

        Assert.Equal(2, service.Count);
        Assert.Equal("history-first", runs[0].RunId);
        Assert.Equal("history-second", runs[1].RunId);
        Assert.True(latest.Found);
        Assert.Equal("history-second", latest.Value!.RunId);
    }

    [Fact]
    public async Task ApiContractExposesProductHistoryReadiness()
    {
        var service = new RadarPulseProductPipelineService();
        var api = new RadarPulseProductPipelineApiContract(service);

        await api.RunDemoAsync(new RadarPulseProductPipelineSyntheticRunRequest("history-api"));

        var response = api.GetHistoryReadiness();

        Assert.True(response.IsSuccess);
        Assert.Equal(200, response.StatusCode);
        Assert.True(response.Body!.IsReady);
        Assert.Equal(RadarPulseProductRunHistoryStorageKind.InMemory, response.Body.StorageKind);
        Assert.Equal(1, response.Body.LoadedRunCount);
    }
}
