using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Infrastructure.Product;

public sealed partial class RadarPulseProductPipelineService : IRadarPulseProductPipelineService
{
    public IReadOnlyList<RadarPulseProductRunSummary> ListRuns()
        => historyStore.ListRuns();

    /// <summary>
    /// Attempts to load one product run detail by run id.
    /// </summary>
    public RadarPulseProductQueryResult<RadarPulseProductRunDetail> TryGetRun(
        string runId)
        => historyStore.TryGetRun(runId);

    /// <summary>
    /// Attempts to load the latest product run detail from history.
    /// </summary>
    public RadarPulseProductQueryResult<RadarPulseProductRunDetail> TryGetLatestRun()
        => historyStore.TryGetLatestRun();

    /// <summary>
    /// Lists all provider batches captured for a product run.
    /// </summary>
    public RadarPulseProductQueryResult<IReadOnlyList<RadarPulseProductBatch>> ListBatches(
        string runId)
    {
        var run = TryGetRun(runId);
        return run.Found
            ? RadarPulseProductQueryResult<IReadOnlyList<RadarPulseProductBatch>>.FromValue(run.Value!.Batches)
            : RadarPulseProductQueryResult<IReadOnlyList<RadarPulseProductBatch>>.NotFound(run.Message);
    }

    /// <summary>
    /// Looks up one batch by provider sequence within a product run.
    /// </summary>
    /// <remarks>
    /// Provider sequence is the stable ordering key from the accepted ordered
    /// commit path, so this lookup does not depend on list position.
    /// </remarks>
    public RadarPulseProductQueryResult<RadarPulseProductBatch> TryGetBatch(
        string runId,
        long providerSequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(providerSequence);

        var run = TryGetRun(runId);
        if (!run.Found)
        {
            return RadarPulseProductQueryResult<RadarPulseProductBatch>.NotFound(run.Message);
        }

        foreach (var batch in run.Value!.Batches)
        {
            if (batch.ProviderSequence == providerSequence)
            {
                return RadarPulseProductQueryResult<RadarPulseProductBatch>.FromValue(batch);
            }
        }

        return RadarPulseProductQueryResult<RadarPulseProductBatch>.NotFound(
            $"Product run '{runId}' does not contain batch sequence {providerSequence}.");
    }

    /// <summary>
}
