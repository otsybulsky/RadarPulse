using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Application.Processing;

/// <summary>
/// Builds BFF read models from processing core state and queued/durable evidence.
/// </summary>
public static class RadarProcessingRunReadModelBuilder
{
    /// <summary>
    /// Creates a run read model from a processing core and optional session/readiness evidence.
    /// </summary>
    public static RadarProcessingRunReadModel FromCore(
        string runId,
        RadarSourceUniverse sourceUniverse,
        RadarProcessingCore core,
        RadarProcessingQueuedSessionResult? sessionResult = null,
        RadarProcessingDurableRuntimeReadinessSummary? readiness = null,
        IReadOnlyList<string>? warnings = null,
        RadarProcessingProviderQueueTelemetrySummary? queueTelemetry = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(sourceUniverse);
        ArgumentNullException.ThrowIfNull(core);

        var handlerContract = RadarProcessingHandlerOutputContract.FromOptions(core.Options);
        var sources = CreateSources(sourceUniverse, core, handlerContract);
        var batches = sessionResult is null
            ? Array.Empty<RadarProcessingBatchReadModel>()
            : CreateBatches(sessionResult);
        var effectiveQueueTelemetry = queueTelemetry ?? sessionResult?.Telemetry;
        var handlerOutputProvenance = CreateHandlerOutputProvenance(handlerContract);
        var diagnostics = new RadarProcessingRunDiagnosticsReadModel(
            ProcessingCompletenessPassed(sessionResult, effectiveQueueTelemetry),
            core.CreateMetrics(),
            effectiveQueueTelemetry,
            readiness,
            warnings,
            handlerOutputProvenance,
            handlerContract.FirstBlockingReason ?? string.Empty);

        return new RadarProcessingRunReadModel(
            runId,
            handlerContract,
            diagnostics,
            batches,
            sources);
    }

    private static IReadOnlyList<RadarProcessingSourceOutputReadModel> CreateSources(
        RadarSourceUniverse sourceUniverse,
        RadarProcessingCore core,
        RadarProcessingHandlerOutputContract handlerContract)
    {
        var snapshots = core.CreateSourceSnapshots();
        var handlerSnapshots = core.CreateSourceHandlerSnapshots();
        if (snapshots.Length != sourceUniverse.SourceCount ||
            handlerSnapshots.Length != sourceUniverse.SourceCount)
        {
            throw new ArgumentException(
                "Source universe must match the processing core source universe.",
                nameof(sourceUniverse));
        }

        var sources = new RadarProcessingSourceOutputReadModel[snapshots.Length];
        for (var sourceId = 0; sourceId < snapshots.Length; sourceId++)
        {
            var snapshot = snapshots[sourceId];
            if (snapshot.SourceId != sourceId)
            {
                throw new ArgumentException(
                    "Processing source snapshots must be dense and sorted by source id.",
                    nameof(core));
            }

            var identity = new RadarProcessingSourceIdentityReadModel(
                sourceId,
                sourceUniverse.GetSourceKey(sourceId));
            sources[sourceId] = new RadarProcessingSourceOutputReadModel(
                identity,
                snapshot.IsActive,
                snapshot.ProcessedEventCount,
                snapshot.ProcessedPayloadValueCount,
                snapshot.RawValueChecksum,
                snapshot.LastMessageTimestampUtcTicks,
                snapshot.ProcessingChecksum,
                CreateHandlerValues(handlerContract, handlerSnapshots[sourceId]));
        }

        return Array.AsReadOnly(sources);
    }

    private static IReadOnlyList<RadarProcessingHandlerOutputValueReadModel> CreateHandlerValues(
        RadarProcessingHandlerOutputContract handlerContract,
        RadarSourceProcessingHandlerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(handlerContract);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!handlerContract.HasHandlers)
        {
            return Array.Empty<RadarProcessingHandlerOutputValueReadModel>();
        }

        var fields = handlerContract.Handlers
            .SelectMany(static handler => handler.Fields)
            .ToArray();
        if (fields.Length != snapshot.Values.Count)
        {
            throw new ArgumentException(
                "Handler output contract field count must match source handler snapshot values.",
                nameof(snapshot));
        }

        var values = new RadarProcessingHandlerOutputValueReadModel[fields.Length];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = RadarProcessingHandlerOutputValueReadModel.FromSnapshotValue(
                fields[i],
                snapshot.Values[i]);
        }

        return Array.AsReadOnly(values);
    }

    private static IReadOnlyList<RadarProcessingBatchReadModel> CreateBatches(
        RadarProcessingQueuedSessionResult sessionResult)
    {
        ArgumentNullException.ThrowIfNull(sessionResult);

        var acceptedBySequence = new Dictionary<long, RadarProcessingQueuedBatch>();
        foreach (var enqueue in sessionResult.EnqueueResults)
        {
            if (enqueue.IsAccepted)
            {
                var batch = enqueue.Batch!;
                acceptedBySequence.Add(batch.Sequence.Value, batch);
            }
        }

        var processingBySequence = new Dictionary<long, RadarProcessingQueuedBatchProcessingResult>();
        foreach (var processing in sessionResult.ProcessingResults)
        {
            processingBySequence.Add(processing.Sequence.Value, processing);
        }

        var sequences = acceptedBySequence.Keys
            .Concat(processingBySequence.Keys)
            .Distinct()
            .Order()
            .ToArray();
        var batches = new RadarProcessingBatchReadModel[sequences.Length];
        for (var i = 0; i < sequences.Length; i++)
        {
            var sequence = sequences[i];
            acceptedBySequence.TryGetValue(sequence, out var accepted);
            processingBySequence.TryGetValue(sequence, out var processing);
            batches[i] = new RadarProcessingBatchReadModel(
                sequence,
                wasAccepted: accepted is not null,
                accepted?.StreamEventCount ?? 0,
                accepted?.PayloadBytes ?? 0,
                accepted?.PayloadValueCount ?? 0,
                accepted?.RawValueChecksum ?? 0,
                processing?.Status,
                processing?.Message ?? string.Empty,
                processing?.TopologyVersion,
                processing?.ProcessingResult?.Metrics);
        }

        return Array.AsReadOnly(batches);
    }

    private static RadarProcessingHandlerOutputProvenance CreateHandlerOutputProvenance(
        RadarProcessingHandlerOutputContract handlerContract)
    {
        ArgumentNullException.ThrowIfNull(handlerContract);

        return handlerContract.StatePosture switch
        {
            RadarProcessingHandlerStatePosture.HandlerFreeOrderedConcurrent =>
                RadarProcessingHandlerOutputProvenance.HandlerFreeOrderedConcurrent,
            RadarProcessingHandlerStatePosture.StatefulSnapshotSequentialFallback =>
                RadarProcessingHandlerOutputProvenance.StatefulSequentialFallback,
            RadarProcessingHandlerStatePosture.MergeableHandlerDeltaMergeEligible =>
                RadarProcessingHandlerOutputProvenance.OrderedHandlerDeltaMerge,
            RadarProcessingHandlerStatePosture.UnsupportedHandlerSet =>
                RadarProcessingHandlerOutputProvenance.UnsupportedHandlerSet,
            _ => throw new ArgumentOutOfRangeException(nameof(handlerContract))
        };
    }

    private static bool ProcessingCompletenessPassed(
        RadarProcessingQueuedSessionResult? sessionResult,
        RadarProcessingProviderQueueTelemetrySummary? queueTelemetry)
    {
        if (sessionResult is null)
        {
            return true;
        }

        var acceptedCount = (long)sessionResult.EnqueueResults.Count(static result => result.IsAccepted);
        if (acceptedCount == 0 && queueTelemetry is not null)
        {
            acceptedCount = queueTelemetry.EnqueuedBatchCount;
        }

        return sessionResult.IsCompleted &&
               acceptedCount == sessionResult.ProcessingResults.Count(static result => result.IsSuccessful);
    }
}
