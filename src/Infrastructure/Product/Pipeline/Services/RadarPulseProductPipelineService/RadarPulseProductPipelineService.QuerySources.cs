using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Infrastructure.Product;

public sealed partial class RadarPulseProductPipelineService : IRadarPulseProductPipelineService
{
    public RadarPulseProductQueryResult<IReadOnlyList<RadarPulseProductSource>> ListSources(
        string runId)
    {
        var run = TryGetRun(runId);
        return run.Found
            ? RadarPulseProductQueryResult<IReadOnlyList<RadarPulseProductSource>>.FromValue(run.Value!.Sources)
            : RadarPulseProductQueryResult<IReadOnlyList<RadarPulseProductSource>>.NotFound(run.Message);
    }

    /// <summary>
    /// Looks up one processed source by dense source id within a product run.
    /// </summary>
    public RadarPulseProductQueryResult<RadarPulseProductSource> TryGetSource(
        string runId,
        int sourceId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sourceId);

        var run = TryGetRun(runId);
        if (!run.Found)
        {
            return RadarPulseProductQueryResult<RadarPulseProductSource>.NotFound(run.Message);
        }

        foreach (var source in run.Value!.Sources)
        {
            if (source.Identity.SourceId == sourceId)
            {
                return RadarPulseProductQueryResult<RadarPulseProductSource>.FromValue(source);
            }
        }

        return RadarPulseProductQueryResult<RadarPulseProductSource>.NotFound(
            $"Product run '{runId}' does not contain source {sourceId}.");
    }

    /// <summary>
    /// Looks up one exported handler output field for a source in a product run.
    /// </summary>
    public RadarPulseProductQueryResult<RadarPulseProductHandlerOutput> TryGetHandlerOutput(
        string runId,
        int sourceId,
        string fieldName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        var source = TryGetSource(runId, sourceId);
        if (!source.Found)
        {
            return RadarPulseProductQueryResult<RadarPulseProductHandlerOutput>.NotFound(source.Message);
        }

        foreach (var value in source.Value!.HandlerValues)
        {
            if (string.Equals(value.Name, fieldName, StringComparison.Ordinal))
            {
                return RadarPulseProductQueryResult<RadarPulseProductHandlerOutput>.FromValue(value);
            }
        }

        return RadarPulseProductQueryResult<RadarPulseProductHandlerOutput>.NotFound(
            $"Product run '{runId}' source {sourceId} does not contain handler field '{fieldName}'.");
    }

    /// <summary>
    /// Returns diagnostic evidence for a product run when diagnostics were captured.
    /// </summary>
    public RadarPulseProductQueryResult<RadarPulseProductDiagnostics> TryGetDiagnostics(
        string runId)
    {
        var run = TryGetRun(runId);
        if (!run.Found)
        {
            return RadarPulseProductQueryResult<RadarPulseProductDiagnostics>.NotFound(run.Message);
        }

        return run.Value!.Diagnostics is { } diagnostics
            ? RadarPulseProductQueryResult<RadarPulseProductDiagnostics>.FromValue(diagnostics)
            : RadarPulseProductQueryResult<RadarPulseProductDiagnostics>.NotFound(
                $"Product run '{runId}' does not have diagnostics.");
    }

    /// <summary>
    /// Returns capacity and completeness evidence for a product run.
    /// </summary>
    public RadarPulseProductQueryResult<RadarPulseProductCapacityEvidence> TryGetCapacityEvidence(
        string runId)
    {
        var run = TryGetRun(runId);
        return run.Found
            ? RadarPulseProductQueryResult<RadarPulseProductCapacityEvidence>.FromValue(
                run.Value!.CapacityEvidence)
            : RadarPulseProductQueryResult<RadarPulseProductCapacityEvidence>.NotFound(run.Message);
    }

    /// <summary>
    /// Applies a product control action against recoverable durable pipeline state.
    /// </summary>
    /// <remarks>
    /// The method reconstructs the source universe and accepted handler/options
    /// contour from the request, then delegates to the production pipeline control
}
