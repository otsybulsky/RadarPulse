using RadarPulse.Domain.Processing;

namespace RadarPulse.Application.Processing;

/// <summary>
/// BFF-facing aggregate read model for one processing run.
/// </summary>
/// <remarks>
/// The read model keeps handler contract, diagnostics, batch evidence, and source
/// output together so product-facing adapters can query without rehydrating the
/// processing core.
/// </remarks>
public sealed class RadarProcessingRunReadModel
{
    private readonly IReadOnlyList<RadarProcessingBatchReadModel> batches;
    private readonly IReadOnlyList<RadarProcessingSourceOutputReadModel> sources;

    /// <summary>
    /// Creates a run read model with sorted batches and dense source output.
    /// </summary>
    public RadarProcessingRunReadModel(
        string runId,
        RadarProcessingHandlerOutputContract handlerOutputContract,
        RadarProcessingRunDiagnosticsReadModel diagnostics,
        IReadOnlyList<RadarProcessingBatchReadModel>? batches = null,
        IReadOnlyList<RadarProcessingSourceOutputReadModel>? sources = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(handlerOutputContract);
        ArgumentNullException.ThrowIfNull(diagnostics);

        RunId = runId;
        HandlerOutputContract = handlerOutputContract;
        Diagnostics = diagnostics;
        this.batches = CopyBatches(batches);
        this.sources = CopySources(sources);
    }

    /// <summary>
    /// Product/runtime run id.
    /// </summary>
    public string RunId { get; }

    /// <summary>
    /// Handler output contract for the run.
    /// </summary>
    public RadarProcessingHandlerOutputContract HandlerOutputContract { get; }

    /// <summary>
    /// Diagnostics and readiness posture for the run.
    /// </summary>
    public RadarProcessingRunDiagnosticsReadModel Diagnostics { get; }

    /// <summary>
    /// Batch evidence sorted by provider sequence.
    /// </summary>
    public IReadOnlyList<RadarProcessingBatchReadModel> Batches => batches;

    /// <summary>
    /// Source output sorted densely by source id.
    /// </summary>
    public IReadOnlyList<RadarProcessingSourceOutputReadModel> Sources => sources;

    /// <summary>
    /// Indicates whether run diagnostics are ready.
    /// </summary>
    public bool IsReady => Diagnostics.IsReady;

    /// <summary>
    /// Attempts to find a batch by provider sequence.
    /// </summary>
    public bool TryGetBatch(
        long providerSequence,
        out RadarProcessingBatchReadModel? batch)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(providerSequence);
        foreach (var candidate in batches)
        {
            if (candidate.ProviderSequence == providerSequence)
            {
                batch = candidate;
                return true;
            }
        }

        batch = null;
        return false;
    }

    /// <summary>
    /// Attempts to find source output by source id.
    /// </summary>
    public bool TryGetSource(
        int sourceId,
        out RadarProcessingSourceOutputReadModel? source)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sourceId);
        foreach (var candidate in sources)
        {
            if (candidate.SourceId == sourceId)
            {
                source = candidate;
                return true;
            }
        }

        source = null;
        return false;
    }

    private static IReadOnlyList<RadarProcessingBatchReadModel> CopyBatches(
        IReadOnlyList<RadarProcessingBatchReadModel>? batches)
    {
        if (batches is null || batches.Count == 0)
        {
            return Array.Empty<RadarProcessingBatchReadModel>();
        }

        var result = new RadarProcessingBatchReadModel[batches.Count];
        var previousSequence = -1L;
        for (var i = 0; i < batches.Count; i++)
        {
            var batch = batches[i] ?? throw new ArgumentNullException(nameof(batches));
            if (batch.ProviderSequence <= previousSequence)
            {
                throw new ArgumentException(
                    "Batch read models must be sorted by provider sequence.",
                    nameof(batches));
            }

            result[i] = batch;
            previousSequence = batch.ProviderSequence;
        }

        return Array.AsReadOnly(result);
    }

    private static IReadOnlyList<RadarProcessingSourceOutputReadModel> CopySources(
        IReadOnlyList<RadarProcessingSourceOutputReadModel>? sources)
    {
        if (sources is null || sources.Count == 0)
        {
            return Array.Empty<RadarProcessingSourceOutputReadModel>();
        }

        var result = new RadarProcessingSourceOutputReadModel[sources.Count];
        for (var i = 0; i < sources.Count; i++)
        {
            var source = sources[i] ?? throw new ArgumentNullException(nameof(sources));
            if (source.SourceId != i)
            {
                throw new ArgumentException(
                    "Source read models must be dense and sorted by source id.",
                    nameof(sources));
            }

            result[i] = source;
        }

        return Array.AsReadOnly(result);
    }
}
