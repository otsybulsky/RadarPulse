using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

/// <summary>
/// Applies radar event batches to source state and produces deterministic processing metrics.
/// </summary>
/// <remarks>
/// The core owns source state and topology manager state. Async shard transport callers compute
/// work outside the core but still commit through the same validation and state application paths.
/// </remarks>
public sealed class RadarProcessingCore
{
    private readonly RadarSourceUniverse sourceUniverse;
    private readonly RadarProcessingTopologyManager topologyManager;
    private readonly RadarSourceProcessingStateStore stateStore;
    private readonly object asyncHandlerStateSync = new();
    private long processedBatchCount;

    /// <summary>
    /// Creates a processing core for a source universe and optional execution configuration.
    /// </summary>
    public RadarProcessingCore(
        RadarSourceUniverse sourceUniverse,
        RadarProcessingCoreOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sourceUniverse);

        options ??= RadarProcessingCoreOptions.Default;

        this.sourceUniverse = sourceUniverse;
        Options = options;
        topologyManager = new RadarProcessingTopologyManager(sourceUniverse, options);
        stateStore = new RadarSourceProcessingStateStore(sourceUniverse, options.HandlerSlotLayout);
    }

    /// <summary>
    /// Gets the immutable configuration used by this core.
    /// </summary>
    public RadarProcessingCoreOptions Options { get; }

    /// <summary>
    /// Gets the current processing topology.
    /// </summary>
    public RadarProcessingTopology Topology => topologyManager.Current;

    internal RadarProcessingTopologyManager TopologyManager => topologyManager;

    internal RadarSourceUniverse SourceUniverse => sourceUniverse;

    /// <summary>
    /// Gets the number of sources in the configured source universe.
    /// </summary>
    public int SourceCount => sourceUniverse.SourceCount;

    /// <summary>
    /// Processes a batch synchronously using the configured sequential or partitioned-barrier mode.
    /// </summary>
    /// <returns>The committed processing result, or an invalid result when validation rejects the batch.</returns>
    public RadarProcessingResult Process(
        RadarEventBatch batch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var invalid = ValidateBatchForProcessing(batch, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        return Options.ExecutionMode switch
        {
            RadarProcessingExecutionMode.Sequential => ProcessSequential(batch, cancellationToken),
            RadarProcessingExecutionMode.PartitionedBarrier => ProcessPartitionedBarrier(batch, cancellationToken),
            RadarProcessingExecutionMode.AsyncShardTransport =>
                throw new NotSupportedException("Async shard transport execution requires RadarProcessingAsyncCoreSession.ProcessAsync."),
            _ => throw new InvalidOperationException("Unsupported processing execution mode.")
        };
    }

    /// <summary>
    /// Gets the current processing snapshot for one source id.
    /// </summary>
    public RadarSourceProcessingSnapshot GetSourceSnapshot(int sourceId) =>
        stateStore.GetSnapshot(sourceId);

    /// <summary>
    /// Creates an ordered snapshot of every source state.
    /// </summary>
    public RadarSourceProcessingSnapshot[] CreateSourceSnapshots() =>
        stateStore.CreateSnapshots();

    /// <summary>
    /// Gets the current handler snapshot for one source id.
    /// </summary>
    public RadarSourceProcessingHandlerSnapshot GetSourceHandlerSnapshot(int sourceId) =>
        stateStore.GetHandlerSnapshot(sourceId);

    /// <summary>
    /// Creates an ordered snapshot of every source handler state.
    /// </summary>
    public RadarSourceProcessingHandlerSnapshot[] CreateSourceHandlerSnapshots() =>
        stateStore.CreateHandlerSnapshots();

    /// <summary>
    /// Creates cumulative metrics from the current source state and processed batch count.
    /// </summary>
    public RadarProcessingMetrics CreateMetrics() =>
        stateStore.CreateMetrics(processedBatchCount);

    /// <summary>
    /// Captures source state for a topology partition.
    /// </summary>
    public RadarProcessingPartitionStateSnapshot CapturePartitionState(
        RadarProcessingPartitionAssignment partition) =>
        RadarProcessingPartitionStateSnapshot.Capture(partition, stateStore);

    /// <summary>
    /// Computes a handler-free ordered concurrent processing delta without committing it.
    /// </summary>
    /// <remarks>
    /// The returned delta owns pooled arrays and must be disposed by the caller after commit or rejection.
    /// </remarks>
    public RadarProcessingBatchDelta ComputeProcessingDelta(
        RadarEventBatch batch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (Options.Handlers.Count != 0)
        {
            throw new NotSupportedException(
                "Ordered concurrent processing deltas require a handler-free processing core.");
        }

        var invalid = ValidateBatchForProcessing(batch, cancellationToken);
        if (invalid is not null)
        {
            throw new InvalidOperationException(invalid.Validation.Message);
        }

        var route = new RadarProcessingBatchRouter(Topology).Route(batch);
        return RadarProcessingBatchDelta.Create(batch, route, sourceUniverse.SourceCount);
    }

    /// <summary>
    /// Computes a processing delta for handler delta merge without mutating committed source state.
    /// </summary>
    public RadarProcessingBatchDelta ComputeProcessingDeltaForHandlerDeltaMerge(
        RadarEventBatch batch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var invalid = ValidateBatchForProcessing(batch, cancellationToken);
        if (invalid is not null)
        {
            throw new InvalidOperationException(invalid.Validation.Message);
        }

        var route = new RadarProcessingBatchRouter(Topology).Route(batch);
        return RadarProcessingBatchDelta.Create(batch, route, sourceUniverse.SourceCount);
    }

    /// <summary>
    /// Validates and commits a previously computed processing delta to source state.
    /// </summary>
    /// <returns>A valid committed result, or an invalid result when state validation rejects the delta.</returns>
    public RadarProcessingResult CommitProcessingDelta(
        RadarProcessingBatchDelta delta,
        RadarProcessingWorkerTelemetrySummary? workerTelemetry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delta);
        cancellationToken.ThrowIfCancellationRequested();

        if (delta.Route.TopologyVersion != Topology.Version)
        {
            throw new InvalidOperationException(
                "Processing delta topology version must match the current processing topology.");
        }

        var invalid = stateStore.ValidateDeltaForCommit(
            delta,
            Options,
            Topology.Version,
            processedBatchCount);
        if (invalid is not null)
        {
            return invalid;
        }

        stateStore.ApplyDelta(delta);
        processedBatchCount = checked(processedBatchCount + 1);

        var telemetry = Options.ExecutionMode == RadarProcessingExecutionMode.Sequential
            ? null
            : RadarProcessingTelemetry.FromRoute(Options.ExecutionMode, delta.Route);
        return Valid(telemetry, workerTelemetry);
    }

    /// <summary>
    /// Validates a previously computed processing delta before ordered commit.
    /// </summary>
    public RadarProcessingResult? ValidateProcessingDeltaForCommit(
        RadarProcessingBatchDelta delta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delta);
        cancellationToken.ThrowIfCancellationRequested();

        if (delta.Route.TopologyVersion != Topology.Version)
        {
            throw new InvalidOperationException(
                "Processing delta topology version must match the current processing topology.");
        }

        return stateStore.ValidateDeltaForCommit(
            delta,
            Options,
            Topology.Version,
            processedBatchCount);
    }

    internal RadarProcessingResult CommitValidatedProcessingDeltaWithMergedHandlerValues(
        RadarProcessingBatchDelta delta,
        IReadOnlyList<RadarProcessingHandlerDeltaValue> mergedHandlerValues,
        RadarProcessingWorkerTelemetrySummary? workerTelemetry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delta);
        ArgumentNullException.ThrowIfNull(mergedHandlerValues);
        cancellationToken.ThrowIfCancellationRequested();

        if (delta.Route.TopologyVersion != Topology.Version)
        {
            throw new InvalidOperationException(
                "Processing delta topology version must match the current processing topology.");
        }

        stateStore.ApplyDeltaWithoutHandlers(delta);
        stateStore.ApplyMergedHandlerValues(mergedHandlerValues);
        processedBatchCount = checked(processedBatchCount + 1);

        var telemetry = Options.ExecutionMode == RadarProcessingExecutionMode.Sequential
            ? null
            : RadarProcessingTelemetry.FromRoute(Options.ExecutionMode, delta.Route);
        return Valid(telemetry, workerTelemetry);
    }

    /// <summary>
    /// Commits a validated processing delta together with merged handler value groups.
    /// </summary>
    public RadarProcessingResult CommitValidatedProcessingDeltaWithMergedHandlerValueGroups(
        RadarProcessingBatchDelta delta,
        IReadOnlyList<IReadOnlyList<RadarProcessingHandlerDeltaValue>> mergedHandlerValueGroups,
        RadarProcessingWorkerTelemetrySummary? workerTelemetry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delta);
        ArgumentNullException.ThrowIfNull(mergedHandlerValueGroups);
        cancellationToken.ThrowIfCancellationRequested();

        if (delta.Route.TopologyVersion != Topology.Version)
        {
            throw new InvalidOperationException(
                "Processing delta topology version must match the current processing topology.");
        }

        stateStore.ApplyDeltaWithoutHandlers(delta);
        stateStore.ApplyMergedHandlerValueGroups(mergedHandlerValueGroups);
        processedBatchCount = checked(processedBatchCount + 1);

        var telemetry = Options.ExecutionMode == RadarProcessingExecutionMode.Sequential
            ? null
            : RadarProcessingTelemetry.FromRoute(Options.ExecutionMode, delta.Route);
        return Valid(telemetry, workerTelemetry);
    }

    /// <summary>
    /// Validates a batch before processing and returns an invalid result when rejected.
    /// </summary>
    public RadarProcessingResult? ValidateBatchForProcessing(
        RadarEventBatch batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();

        if (batch.StreamSchemaVersion != StreamSchemaVersion.Current)
        {
            return Invalid(
                RadarProcessingValidationError.UnsupportedStreamSchemaVersion,
                sourceId: -1,
                eventIndex: -1,
                $"Unsupported stream schema version {batch.StreamSchemaVersion}.");
        }

        if (batch.SourceUniverseVersion != sourceUniverse.Version)
        {
            return Invalid(
                RadarProcessingValidationError.SourceUniverseVersionMismatch,
                sourceId: -1,
                eventIndex: -1,
                "Batch source-universe version does not match the processing core source universe.");
        }

        return ValidateSources(batch.Events.Span, cancellationToken);
    }

    /// <summary>
    /// Creates an invalid processing result in the core validation vocabulary.
    /// </summary>
    public RadarProcessingResult CreateInvalidProcessingResult(
        RadarProcessingValidationError error,
        int sourceId,
        int eventIndex,
        string message) =>
        Invalid(error, sourceId, eventIndex, message);

    /// <summary>
    /// Processes one async shard work item against a routed batch.
    /// </summary>
    public RadarProcessingAsyncWorkCompletion ProcessAsyncShardWorkItem(
        RadarEventBatch batch,
        RadarProcessingBatchRoute route,
        RadarProcessingAsyncWorkItem workItem,
        CancellationToken cancellationToken,
        out RadarProcessingResult? invalidResult)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(workItem);

        if (route.TopologyVersion != workItem.TopologyVersion ||
            route.TopologyVersion != Topology.Version)
        {
            throw new ArgumentException("Async work item topology version must match the captured route.", nameof(workItem));
        }

        var shard = route.GetShard(workItem.ShardId);
        var eventIndexes = shard.EventIndexes.Span;
        var events = batch.Events.Span;
        var processedStreamEventCount = 0L;
        var processedPayloadValueCount = 0L;

        for (var i = 0; i < eventIndexes.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var eventIndex = eventIndexes[i];
            var streamEvent = events[eventIndex];
            var payloadMetrics = route.GetRoutedEvent(eventIndex).PayloadMetrics;
            var result = ApplyProcessedEventFromAsyncWorker(
                streamEvent,
                eventIndex,
                batch.Payload.Span,
                payloadMetrics);
            if (result is not null)
            {
                invalidResult = result;
                return RadarProcessingAsyncWorkCompletion.Failed(
                    workItem,
                    failureKind: RadarProcessingAsyncFailureKind.WorkerReportedFailure);
            }

            processedStreamEventCount++;
            processedPayloadValueCount = checked(processedPayloadValueCount + payloadMetrics.PayloadValueCount);
        }

        invalidResult = null;
        return RadarProcessingAsyncWorkCompletion.Succeeded(
            workItem,
            processedStreamEventCount: processedStreamEventCount,
            processedPayloadValueCount: processedPayloadValueCount);
    }

    private RadarProcessingResult? ApplyProcessedEventFromAsyncWorker(
        in RadarStreamEvent streamEvent,
        int eventIndex,
        ReadOnlySpan<byte> batchPayload,
        RadarProcessingPayloadMetrics payloadMetrics)
    {
        if (Options.Handlers.Count == 0)
        {
            return ApplyProcessedEvent(streamEvent, eventIndex, batchPayload, payloadMetrics);
        }

        lock (asyncHandlerStateSync)
        {
            return ApplyProcessedEvent(streamEvent, eventIndex, batchPayload, payloadMetrics);
        }
    }

    /// <summary>
    /// Completes an async batch after shard work has been aggregated.
    /// </summary>
    public RadarProcessingResult CompleteAsyncBatch(
        RadarProcessingTelemetry telemetry,
        RadarProcessingWorkerTelemetrySummary? workerTelemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (Options.ExecutionMode != RadarProcessingExecutionMode.AsyncShardTransport)
        {
            throw new InvalidOperationException("Async batch completion requires async shard transport mode.");
        }

        processedBatchCount = checked(processedBatchCount + 1);
        return Valid(telemetry, workerTelemetry);
    }

    private RadarProcessingResult ProcessSequential(
        RadarEventBatch batch,
        CancellationToken cancellationToken)
    {
        var events = batch.Events.Span;
        var payload = batch.Payload.Span;

        for (var eventIndex = 0; eventIndex < events.Length; eventIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var streamEvent = events[eventIndex];
            var payloadMetrics = RadarProcessingPayloadReader.ComputeEventMetrics(streamEvent, payload);
            var result = ApplyProcessedEvent(streamEvent, eventIndex, payload, payloadMetrics);
            if (result is not null)
            {
                return result;
            }
        }

        processedBatchCount = checked(processedBatchCount + 1);
        return Valid();
    }

    private RadarProcessingResult ProcessPartitionedBarrier(
        RadarEventBatch batch,
        CancellationToken cancellationToken)
    {
        var topology = topologyManager.Current;
        var route = new RadarProcessingBatchRouter(topology).Route(batch);
        var telemetry = RadarProcessingTelemetry.FromRoute(Options.ExecutionMode, route);
        var events = batch.Events.Span;

        foreach (var shard in route.Shards)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var eventIndexes = shard.EventIndexes.Span;
            for (var i = 0; i < eventIndexes.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var eventIndex = eventIndexes[i];
                var streamEvent = events[eventIndex];
                var result = ApplyProcessedEvent(
                    streamEvent,
                    eventIndex,
                    batch.Payload.Span,
                    route.GetRoutedEvent(eventIndex).PayloadMetrics);
                if (result is not null)
                {
                    return result;
                }
            }
        }

        processedBatchCount = checked(processedBatchCount + 1);
        return Valid(telemetry);
    }

    private RadarProcessingResult? ValidateSources(
        ReadOnlySpan<RadarStreamEvent> events,
        CancellationToken cancellationToken)
    {
        for (var eventIndex = 0; eventIndex < events.Length; eventIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = ValidateSource(events[eventIndex], eventIndex);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private RadarProcessingResult? ApplyProcessedEvent(
        RadarStreamEvent streamEvent,
        int eventIndex,
        ReadOnlySpan<byte> batchPayload,
        RadarProcessingPayloadMetrics payloadMetrics)
    {
        try
        {
            var eventPayload = Options.Handlers.Count == 0
                ? ReadOnlySpan<byte>.Empty
                : RadarProcessingPayloadReader.GetEventPayload(streamEvent, batchPayload);
            stateStore.ApplyProcessedEvent(
                streamEvent,
                eventPayload,
                payloadMetrics);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(
                RadarProcessingValidationError.SourceOrderViolation,
                streamEvent.SourceId,
                eventIndex,
                ex.Message);
        }
    }

    private RadarProcessingResult? ValidateSource(
        RadarStreamEvent streamEvent,
        int eventIndex)
    {
        if ((uint)streamEvent.SourceId >= (uint)sourceUniverse.SourceCount)
        {
            return Invalid(
                RadarProcessingValidationError.SourceIdOutsideUniverse,
                streamEvent.SourceId,
                eventIndex,
                "Event SourceId is outside the processing core source universe.");
        }

        var sourceKey = new RadarSourceKey(
            streamEvent.RadarOrdinal,
            streamEvent.ElevationSlot,
            streamEvent.AzimuthBucket,
            streamEvent.RangeBand);
        if (!sourceUniverse.Contains(sourceKey))
        {
            return Invalid(
                RadarProcessingValidationError.SourceOwnershipMismatch,
                streamEvent.SourceId,
                eventIndex,
                "Event source dimensions are outside the processing core source universe.");
        }

        var expectedSourceId = sourceUniverse.GetSourceId(sourceKey);
        if (streamEvent.SourceId != expectedSourceId)
        {
            return Invalid(
                RadarProcessingValidationError.SourceOwnershipMismatch,
                streamEvent.SourceId,
                eventIndex,
                "Event SourceId does not match its source dimensions.");
        }

        return null;
    }

    private RadarProcessingResult Valid(
        RadarProcessingTelemetry? telemetry = null,
        RadarProcessingWorkerTelemetrySummary? workerTelemetry = null)
    {
        var metrics = CreateMetrics();
        return new RadarProcessingResult(
            Options.ExecutionMode,
            Options.PartitionCount,
            Options.ShardCount,
            metrics,
            RadarProcessingValidationResult.Valid(metrics),
            telemetry,
            Topology.Version,
            workerTelemetry);
    }

    private RadarProcessingResult Invalid(
        RadarProcessingValidationError error,
        int sourceId,
        int eventIndex,
        string message)
    {
        var metrics = CreateMetrics();
        return new RadarProcessingResult(
            Options.ExecutionMode,
            Options.PartitionCount,
            Options.ShardCount,
            metrics,
            RadarProcessingValidationResult.Invalid(
                error,
                sourceId,
                eventIndex,
                message,
                metrics),
            topologyVersion: Topology.Version);
    }
}
