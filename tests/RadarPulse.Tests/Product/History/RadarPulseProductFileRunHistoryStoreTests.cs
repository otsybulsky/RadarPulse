using RadarPulse.Application.Product;
using RadarPulse.Infrastructure.Product;

namespace RadarPulse.Tests.Product;

public sealed class RadarPulseProductFileRunHistoryStoreTests
{
    [Fact]
    public async Task FileHistoryStoreSavesAndReloadsProductRunDetail()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.File("history.json");
        var detail = await CreateDetailAsync("file-history-run");

        var store = new RadarPulseProductFileRunHistoryStore(path);
        store.Store(detail);
        var reloaded = new RadarPulseProductFileRunHistoryStore(path);

        var run = reloaded.TryGetRun("file-history-run");

        Assert.True(reloaded.Readiness.IsReady);
        Assert.Equal(RadarPulseProductRunHistoryStorageKind.LocalFile, reloaded.Readiness.StorageKind);
        Assert.True(run.Found);
        Assert.Equal(detail.RunId, run.Value!.RunId);
        Assert.Equal(detail.Summary.BatchCount, run.Value.Summary.BatchCount);
        Assert.True(run.Value.CapacityEvidence.IsReady);
    }

    [Fact]
    public async Task FileHistoryStorePreservesLatestRunOrderingAfterReload()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.File("history.json");
        var first = await CreateDetailAsync("file-history-first");
        var second = await CreateDetailAsync("file-history-second");

        var store = new RadarPulseProductFileRunHistoryStore(path);
        store.Store(first);
        store.Store(second);
        var reloaded = new RadarPulseProductFileRunHistoryStore(path);

        var runs = reloaded.ListRuns();
        var latest = reloaded.TryGetLatestRun();

        Assert.Equal(2, runs.Count);
        Assert.Equal("file-history-first", runs[0].RunId);
        Assert.Equal("file-history-second", runs[1].RunId);
        Assert.True(latest.Found);
        Assert.Equal("file-history-second", latest.Value!.RunId);
    }

    [Fact]
    public async Task DuplicateSameRecordReplayIsIdempotent()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.File("history.json");
        var detail = await CreateDetailAsync("file-history-idempotent");

        var store = new RadarPulseProductFileRunHistoryStore(path);
        store.Store(detail);
        store.Store(detail);

        Assert.Equal(1, store.Count);
        Assert.Single(store.ListRuns());
    }

    [Fact]
    public async Task DuplicateConflictingRunIdentityFailsClosed()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.File("history.json");
        var detail = await CreateDetailAsync("file-history-conflict");
        var conflict = detail with { Message = "conflicting persisted product run" };
        var store = new RadarPulseProductFileRunHistoryStore(path);

        store.Store(detail);

        var exception = Assert.Throws<InvalidOperationException>(() => store.Store(conflict));
        Assert.Contains("conflicting run", exception.Message);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void CorruptJsonReportsBlockedReadiness()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.File("history.json");
        File.WriteAllText(path, "not-json");

        var store = new RadarPulseProductFileRunHistoryStore(path);

        Assert.False(store.Readiness.IsReady);
        Assert.Equal(1, store.Readiness.RejectedRunCount);
        Assert.Contains("JSON is invalid", store.Readiness.FirstBlockingReason);
        Assert.Throws<InvalidOperationException>(
            () => store.Store(CreateUnavailableDetail("blocked-corrupt")));
    }

    [Fact]
    public void UnsupportedSchemaReportsBlockedReadiness()
    {
        using var directory = TemporaryDirectory.Create();
        var path = directory.File("history.json");
        File.WriteAllText(
            path,
            """
            {
              "schemaVersion": 999,
              "runs": []
            }
            """);

        var store = new RadarPulseProductFileRunHistoryStore(path);

        Assert.False(store.Readiness.IsReady);
        Assert.Contains("schema version 999", store.Readiness.FirstBlockingReason);
        Assert.Throws<InvalidOperationException>(
            () => store.Store(CreateUnavailableDetail("blocked-schema")));
    }

    [Fact]
    public void DirectoryStoragePathReportsBlockedReadiness()
    {
        using var directory = TemporaryDirectory.Create();

        var store = new RadarPulseProductFileRunHistoryStore(directory.Path);

        Assert.False(store.Readiness.IsReady);
        Assert.Equal(1, store.Readiness.RejectedRunCount);
        Assert.Contains("must be a file path", store.Readiness.FirstBlockingReason);
    }

    private static async Task<RadarPulseProductRunDetail> CreateDetailAsync(
        string runId)
    {
        var service = new RadarPulseProductPipelineService();
        return await service.RunSyntheticAsync(
            new RadarPulseProductPipelineSyntheticRunRequest(
                runId,
                HandlerSet: RadarPulseProductHandlerSet.CounterChecksum));
    }

    private static RadarPulseProductRunDetail CreateUnavailableDetail(
        string runId)
    {
        var summary = new RadarPulseProductRunSummary(
            runId,
            new RadarPulseProductInputSummary(
                RadarPulseProductInputKind.Synthetic,
                "unavailable",
                "test",
                BatchCount: 0,
                EventCount: 0),
            RadarPulseProductRunState.Blocked,
            IsReady: false,
            HasReadModel: false,
            RadarPulseProductHandlerMode.HandlerFree,
            FirstBlockingReason: "unavailable",
            RadarPulseProductFallbackRecommendation.FixConfiguration,
            BatchCount: 0,
            SourceCount: 0,
            AcceptedBatchCount: 0,
            ProcessedBatchCount: 0,
            CommittedBatchCount: 0,
            WarningCount: 0);
        return new RadarPulseProductRunDetail(
            summary,
            new RadarPulseProductConfiguration(
                "test",
                IsValid: false,
                FirstInvalidOption: "test",
                FirstInvalidReason: "unavailable",
                Values: Array.Empty<RadarPulseProductConfigurationValue>(),
                Warnings: Array.Empty<string>()),
            new RadarPulseProductOperatorSummary(
                RadarPulseProductRunState.Blocked,
                IsReady: false,
                ProcessingComplete: false,
                RadarPulseProductHandlerMode.HandlerFree,
                HasHandlerConflict: false,
                HandlerBlockingReason: string.Empty,
                FirstBlockingReason: "unavailable",
                RadarPulseProductFallbackRecommendation.FixConfiguration,
                FirstBlockingBatchId: null,
                FirstBlockingSequence: null,
                FirstBlockingState: null,
                CurrentRetainedBatchCount: 0,
                CurrentRetainedPayloadBytes: 0,
                ReleaseHealthy: true,
                Warnings: Array.Empty<string>()),
            new RadarPulseProductCapacityEvidence(
                runId,
                "test",
                ElapsedMilliseconds: 0,
                MeasuredAllocatedBytes: 0,
                AcceptedBatchCount: 0,
                ProcessedBatchCount: 0,
                CommittedBatchCount: 0,
                RadarPulseProductHandlerMode.HandlerFree,
                DurableAdapterKind: "none",
                TerminalRetainedBatchCount: 0,
                TerminalRetainedPayloadBytes: 0,
                ProcessingCompletenessPassed: false,
                IsReady: false,
                FirstBlockingReason: "unavailable",
                ConfigurationContour: "test"),
            Diagnostics: null,
            HandlerContract: null,
            Batches: Array.Empty<RadarPulseProductBatch>(),
            Sources: Array.Empty<RadarPulseProductSource>(),
            Message: "unavailable");
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
                "radarpulse-m029-file-history-",
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
