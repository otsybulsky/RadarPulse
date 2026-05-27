using RadarPulse.Application.Product;

namespace RadarPulse.Infrastructure.Product;

public sealed class RadarPulseProductInMemoryRunHistoryStore :
    IRadarPulseProductRunHistoryStore
{
    private readonly object sync = new();
    private readonly Dictionary<string, RadarPulseProductRunDetail> runsById = new(StringComparer.Ordinal);
    private readonly List<string> runOrder = new();

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
