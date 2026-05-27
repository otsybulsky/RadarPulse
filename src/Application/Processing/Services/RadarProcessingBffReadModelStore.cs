namespace RadarPulse.Application.Processing;

/// <summary>
/// In-memory store for processing run read models exposed to product/BFF adapters.
/// </summary>
/// <remarks>
/// The store is thread-safe and version-orders published runs by insertion. It is
/// a local read-model cache, not a durable persistence adapter.
/// </remarks>
public sealed class RadarProcessingBffReadModelStore
{
    private readonly object sync = new();
    private readonly Dictionary<string, StoredRun> byRunId = new(StringComparer.Ordinal);
    private long nextVersion;

    /// <summary>
    /// Number of runs currently retained.
    /// </summary>
    public int Count
    {
        get
        {
            lock (sync)
            {
                return byRunId.Count;
            }
        }
    }

    /// <summary>
    /// Publishes or replaces one run read model.
    /// </summary>
    public void Publish(
        RadarProcessingRunReadModel run)
    {
        ArgumentNullException.ThrowIfNull(run);

        lock (sync)
        {
            byRunId[run.RunId] = new StoredRun(run, checked(nextVersion++));
        }
    }

    /// <summary>
    /// Lists retained runs in publication order.
    /// </summary>
    public IReadOnlyList<RadarProcessingRunReadModel> ListRuns()
    {
        lock (sync)
        {
            if (byRunId.Count == 0)
            {
                return Array.Empty<RadarProcessingRunReadModel>();
            }

            return Array.AsReadOnly(
                byRunId.Values
                    .OrderBy(static stored => stored.Version)
                    .Select(static stored => stored.Run)
                    .ToArray());
        }
    }

    /// <summary>
    /// Attempts to return the most recently published run.
    /// </summary>
    public bool TryGetLatestRun(
        out RadarProcessingRunReadModel? run)
    {
        lock (sync)
        {
            if (byRunId.Count == 0)
            {
                run = null;
                return false;
            }

            run = byRunId.Values
                .OrderByDescending(static stored => stored.Version)
                .First()
                .Run;
            return true;
        }
    }

    /// <summary>
    /// Attempts to return one run by run id.
    /// </summary>
    public bool TryGetRun(
        string runId,
        out RadarProcessingRunReadModel? run)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        lock (sync)
        {
            if (byRunId.TryGetValue(runId, out var stored))
            {
                run = stored.Run;
                return true;
            }

            run = null;
            return false;
        }
    }

    /// <summary>
    /// Lists batches for a run, or an empty list when the run is missing.
    /// </summary>
    public IReadOnlyList<RadarProcessingBatchReadModel> ListBatches(
        string runId)
    {
        return TryGetRun(runId, out var run)
            ? run!.Batches
            : Array.Empty<RadarProcessingBatchReadModel>();
    }

    /// <summary>
    /// Attempts to return one batch by run id and provider sequence.
    /// </summary>
    public bool TryGetBatch(
        string runId,
        long providerSequence,
        out RadarProcessingBatchReadModel? batch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentOutOfRangeException.ThrowIfNegative(providerSequence);

        if (TryGetRun(runId, out var run))
        {
            return run!.TryGetBatch(providerSequence, out batch);
        }

        batch = null;
        return false;
    }

    /// <summary>
    /// Lists source output for a run, or an empty list when the run is missing.
    /// </summary>
    public IReadOnlyList<RadarProcessingSourceOutputReadModel> ListSources(
        string runId)
    {
        return TryGetRun(runId, out var run)
            ? run!.Sources
            : Array.Empty<RadarProcessingSourceOutputReadModel>();
    }

    /// <summary>
    /// Attempts to return one source output by run id and source id.
    /// </summary>
    public bool TryGetSource(
        string runId,
        int sourceId,
        out RadarProcessingSourceOutputReadModel? source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceId);

        if (TryGetRun(runId, out var run))
        {
            return run!.TryGetSource(sourceId, out source);
        }

        source = null;
        return false;
    }

    /// <summary>
    /// Attempts to return one handler output value by run, source, and field name.
    /// </summary>
    public bool TryGetHandlerOutput(
        string runId,
        int sourceId,
        string fieldName,
        out RadarProcessingHandlerOutputValueReadModel? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        if (TryGetSource(runId, sourceId, out var source))
        {
            foreach (var candidate in source!.HandlerValues)
            {
                if (string.Equals(candidate.Name, fieldName, StringComparison.Ordinal))
                {
                    value = candidate;
                    return true;
                }
            }
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Attempts to return the handler output contract for a run.
    /// </summary>
    public bool TryGetHandlerOutputContract(
        string runId,
        out RadarProcessingHandlerOutputContract? contract)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        if (TryGetRun(runId, out var run))
        {
            contract = run!.HandlerOutputContract;
            return true;
        }

        contract = null;
        return false;
    }

    /// <summary>
    /// Attempts to return diagnostics for a run.
    /// </summary>
    public bool TryGetDiagnostics(
        string runId,
        out RadarProcessingRunDiagnosticsReadModel? diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        if (TryGetRun(runId, out var run))
        {
            diagnostics = run!.Diagnostics;
            return true;
        }

        diagnostics = null;
        return false;
    }

    private readonly record struct StoredRun(
        RadarProcessingRunReadModel Run,
        long Version);
}
