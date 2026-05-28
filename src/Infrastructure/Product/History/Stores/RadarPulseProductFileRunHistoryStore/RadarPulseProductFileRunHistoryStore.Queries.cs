using RadarPulse.Application.Product;

namespace RadarPulse.Infrastructure.Product;

public sealed partial class RadarPulseProductFileRunHistoryStore
{
    /// <summary>
    /// Lists loaded run summaries in persisted insertion order.
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
    /// Gets one loaded run detail by run id.
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
    /// Gets the most recently loaded or stored run detail.
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
}
