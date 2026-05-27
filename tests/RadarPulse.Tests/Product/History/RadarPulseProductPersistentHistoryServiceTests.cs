using RadarPulse.Application.Product;
using RadarPulse.Infrastructure.Product;

namespace RadarPulse.Tests.Product;

public sealed class RadarPulseProductPersistentHistoryServiceTests
{
    [Fact]
    public async Task ProductServicePersistsRunHistoryAcrossRecreation()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.File("history.json");
        var service = RadarPulseProductPipelineService.CreateWithFileHistory(path);

        await service.RunSyntheticAsync(
            new RadarPulseProductPipelineSyntheticRunRequest("persistent-service-run"));
        var recreated = RadarPulseProductPipelineService.CreateWithFileHistory(path);

        var runs = recreated.ListRuns();
        var latest = recreated.TryGetLatestRun();
        var detail = recreated.TryGetRun("persistent-service-run");

        Assert.Single(runs);
        Assert.Equal("persistent-service-run", runs[0].RunId);
        Assert.True(latest.Found);
        Assert.Equal("persistent-service-run", latest.Value!.RunId);
        Assert.True(detail.Found);
        Assert.True(detail.Value!.IsReady);
    }

    [Fact]
    public async Task ReloadedHistoryKeepsDiagnosticsHandlerOutputAndCapacity()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.File("history.json");
        var service = RadarPulseProductPipelineService.CreateWithFileHistory(path);

        await service.RunSyntheticAsync(
            new RadarPulseProductPipelineSyntheticRunRequest(
                "persistent-handler-run",
                HandlerSet: RadarPulseProductHandlerSet.CounterChecksum));
        var recreated = RadarPulseProductPipelineService.CreateWithFileHistory(path);

        var diagnostics = recreated.TryGetDiagnostics("persistent-handler-run");
        var handler = recreated.TryGetHandlerOutput(
            "persistent-handler-run",
            sourceId: 0,
            fieldName: "benchmark.events");
        var capacity = recreated.TryGetCapacityEvidence("persistent-handler-run");

        Assert.True(diagnostics.Found);
        Assert.True(diagnostics.Value!.UsesOrderedHandlerDeltaMerge);
        Assert.True(handler.Found);
        Assert.Equal("benchmark.events", handler.Value!.Name);
        Assert.True(capacity.Found);
        Assert.True(capacity.Value!.IsReady);
    }

    [Fact]
    public async Task BlockedHistoryStoreFailsRunBeforeClaimingHistory()
    {
        using var directory = TemporaryDirectory.Create();
        var service = RadarPulseProductPipelineService.CreateWithFileHistory(directory.Path);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.RunSyntheticAsync(
                new RadarPulseProductPipelineSyntheticRunRequest("persistent-blocked")));

        Assert.Contains("must be a file path", exception.Message);
        Assert.Equal(0, service.Count);
        Assert.False(service.HistoryReadiness.IsReady);
    }

    [Fact]
    public async Task ApiHistoryReadinessMapsHealthyAndBlockedPersistentStores()
    {
        using var healthyDirectory = TemporaryDirectory.Create();
        using var blockedDirectory = TemporaryDirectory.Create();
        var healthyService = RadarPulseProductPipelineService.CreateWithFileHistory(
            healthyDirectory.File("history.json"));
        var blockedService = RadarPulseProductPipelineService.CreateWithFileHistory(
            blockedDirectory.Path);
        var healthyApi = new RadarPulseProductPipelineApiContract(healthyService);
        var blockedApi = new RadarPulseProductPipelineApiContract(blockedService);

        await healthyApi.RunDemoAsync(
            new RadarPulseProductPipelineSyntheticRunRequest("persistent-api"));
        var healthy = healthyApi.GetHistoryReadiness();
        var blocked = blockedApi.GetHistoryReadiness();

        Assert.True(healthy.IsSuccess);
        Assert.True(healthy.Body!.IsReady);
        Assert.Equal(RadarPulseProductRunHistoryStorageKind.LocalFile, healthy.Body.StorageKind);
        Assert.Equal(1, healthy.Body.LoadedRunCount);
        Assert.True(blocked.IsSuccess);
        Assert.False(blocked.Body!.IsReady);
        Assert.Contains("must be a file path", blocked.Body.FirstBlockingReason);
    }

    private sealed class TemporaryDirectory :
        IDisposable
    {
        private TemporaryDirectory(
            string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "radarpulse-m029-persistent-service-",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public string File(
            string fileName) =>
            System.IO.Path.Combine(Path, fileName);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
