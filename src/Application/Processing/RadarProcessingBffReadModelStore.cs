namespace RadarPulse.Application.Processing;

public sealed class RadarProcessingBffReadModelStore
{
    private readonly object sync = new();
    private readonly Dictionary<string, StoredRun> byRunId = new(StringComparer.Ordinal);
    private long nextVersion;

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

    public void Publish(
        RadarProcessingRunReadModel run)
    {
        ArgumentNullException.ThrowIfNull(run);

        lock (sync)
        {
            byRunId[run.RunId] = new StoredRun(run, checked(nextVersion++));
        }
    }

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

    public IReadOnlyList<RadarProcessingBatchReadModel> ListBatches(
        string runId)
    {
        return TryGetRun(runId, out var run)
            ? run!.Batches
            : Array.Empty<RadarProcessingBatchReadModel>();
    }

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

    public IReadOnlyList<RadarProcessingSourceOutputReadModel> ListSources(
        string runId)
    {
        return TryGetRun(runId, out var run)
            ? run!.Sources
            : Array.Empty<RadarProcessingSourceOutputReadModel>();
    }

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

