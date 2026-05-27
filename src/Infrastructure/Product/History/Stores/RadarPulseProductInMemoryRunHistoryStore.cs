using RadarPulse.Application.Product;

namespace RadarPulse.Infrastructure.Product;

/// <summary>
/// In-memory product run history store used by local demo and test hosts when persistent history is not required.
/// </summary>
public sealed class RadarPulseProductInMemoryRunHistoryStore :
    IRadarPulseProductRunHistoryStore
{
    private readonly object sync = new();
    private readonly Dictionary<string, RadarPulseProductRunDetail> runsById = new(StringComparer.Ordinal);
    private readonly List<string> runOrder = new();

    /// <summary>
    /// Gets the number of stored product runs.
    /// </summary>
    public int Count
    {
        get
        {
            lock (sync)
            {
                return runsById.Count;
            }
        }
    }

    /// <summary>
    /// Gets readiness for the in-memory history store.
    /// </summary>
    public RadarPulseProductRunHistoryReadiness Readiness
    {
        get
        {
            lock (sync)
            {
                return new RadarPulseProductRunHistoryReadiness(
                    RadarPulseProductRunHistoryStorageKind.InMemory,
                    IsReady: true,
                    StorageIdentity: "in-memory",
                    SchemaVersion: 1,
                    LoadedRunCount: runsById.Count,
                    RejectedRunCount: 0,
                    FirstBlockingReason: string.Empty,
                    Warnings: Array.Empty<string>());
            }
        }
    }

    /// <summary>
    /// Lists run summaries in insertion order.
    /// </summary>
    public IReadOnlyList<RadarPulseProductRunSummary> ListRuns()
    {
        lock (sync)
        {
            if (runOrder.Count == 0)
            {
                return Array.Empty<RadarPulseProductRunSummary>();
            }

            return Array.AsReadOnly(
                runOrder
                    .Select(runId => runsById[runId].Summary)
                    .ToArray());
        }
    }

    /// <summary>
    /// Gets one run detail by run id.
    /// </summary>
    public RadarPulseProductQueryResult<RadarPulseProductRunDetail> TryGetRun(
        string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        lock (sync)
        {
            return runsById.TryGetValue(runId, out var detail)
                ? RadarPulseProductQueryResult<RadarPulseProductRunDetail>.FromValue(detail)
                : RadarPulseProductQueryResult<RadarPulseProductRunDetail>.NotFound(
                    $"Product run '{runId}' was not found.");
        }
    }

    /// <summary>
    /// Gets the most recently stored run detail.
    /// </summary>
    public RadarPulseProductQueryResult<RadarPulseProductRunDetail> TryGetLatestRun()
    {
        lock (sync)
        {
            if (runOrder.Count == 0)
            {
                return RadarPulseProductQueryResult<RadarPulseProductRunDetail>.NotFound(
                    "No product pipeline run has been published.");
            }

            return RadarPulseProductQueryResult<RadarPulseProductRunDetail>.FromValue(
                runsById[runOrder[^1]]);
        }
    }

    /// <summary>
    /// Stores or replaces a product run detail in memory.
    /// </summary>
    public void Store(
        RadarPulseProductRunDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail.RunId);

        lock (sync)
        {
            if (!runsById.ContainsKey(detail.RunId))
            {
                runOrder.Add(detail.RunId);
            }

            runsById[detail.RunId] = detail;
        }
    }
}
