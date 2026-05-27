namespace RadarPulse.Application.Product;

/// <summary>
/// Identifies the backing store used for product run history.
/// </summary>
public enum RadarPulseProductRunHistoryStorageKind
{
    /// <summary>
    /// Process-local history that is discarded when the service instance is recreated.
    /// </summary>
    InMemory = 1,

    /// <summary>
    /// Deterministic local JSON history used by the accepted local demo package.
    /// </summary>
    LocalFile = 2
}

/// <summary>
/// Readiness and load posture for the configured product run history store.
/// </summary>
/// <remarks>
/// The readiness shape is exposed through product host routes so scripts and
/// operators can detect missing, unreadable, or partially rejected local history
/// without starting a new run.
/// </remarks>
public sealed record RadarPulseProductRunHistoryReadiness(
    RadarPulseProductRunHistoryStorageKind StorageKind,
    bool IsReady,
    string StorageIdentity,
    int SchemaVersion,
    int LoadedRunCount,
    int RejectedRunCount,
    string FirstBlockingReason,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Store contract for product run summaries and details.
/// </summary>
/// <remarks>
/// Implementations are expected to return immutable snapshots or otherwise
/// protect stored run details from caller mutation. The accepted production-shaped
/// local demo path uses either in-memory state or deterministic local file-backed
/// JSON, not an external database.
/// </remarks>
public interface IRadarPulseProductRunHistoryStore
{
    /// <summary>
    /// Number of run details currently visible through the store.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Current readiness and load posture for the backing store.
    /// </summary>
    RadarPulseProductRunHistoryReadiness Readiness { get; }

    /// <summary>
    /// Lists compact run summaries in the store's presentation order.
    /// </summary>
    IReadOnlyList<RadarPulseProductRunSummary> ListRuns();

    /// <summary>
    /// Attempts to load one persisted run detail by product run id.
    /// </summary>
    RadarPulseProductQueryResult<RadarPulseProductRunDetail> TryGetRun(
        string runId);

    /// <summary>
    /// Attempts to load the latest run detail according to the store's ordering.
    /// </summary>
    RadarPulseProductQueryResult<RadarPulseProductRunDetail> TryGetLatestRun();

    /// <summary>
    /// Stores or replaces a complete product run detail.
    /// </summary>
    /// <remarks>
    /// The persisted detail is the product-facing aggregate, so history reloads do
    /// not need to recompute backend processing, diagnostics, or capacity evidence.
    /// </remarks>
    void Store(
        RadarPulseProductRunDetail detail);
}
