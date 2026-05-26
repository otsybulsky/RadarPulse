namespace RadarPulse.Application.Product;

public enum RadarPulseProductRunHistoryStorageKind
{
    InMemory = 1,
    LocalFile = 2
}

public sealed record RadarPulseProductRunHistoryReadiness(
    RadarPulseProductRunHistoryStorageKind StorageKind,
    bool IsReady,
    string StorageIdentity,
    int SchemaVersion,
    int LoadedRunCount,
    int RejectedRunCount,
    string FirstBlockingReason,
    IReadOnlyList<string> Warnings);

public interface IRadarPulseProductRunHistoryStore
{
    int Count { get; }

    RadarPulseProductRunHistoryReadiness Readiness { get; }

    IReadOnlyList<RadarPulseProductRunSummary> ListRuns();

    RadarPulseProductQueryResult<RadarPulseProductRunDetail> TryGetRun(
        string runId);

    RadarPulseProductQueryResult<RadarPulseProductRunDetail> TryGetLatestRun();

    void Store(
        RadarPulseProductRunDetail detail);
}
