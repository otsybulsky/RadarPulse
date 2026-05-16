using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingCore
{
    private readonly RadarSourceUniverse sourceUniverse;
    private readonly RadarProcessingTopologyManager topologyManager;
    private readonly RadarSourceProcessingStateStore stateStore;
    private long processedBatchCount;

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

    public RadarProcessingCoreOptions Options { get; }

    public RadarProcessingTopology Topology => topologyManager.Current;

    internal RadarProcessingTopologyManager TopologyManager => topologyManager;

    public RadarProcessingResult Process(
        RadarEventBatch batch,
        CancellationToken cancellationToken = default)
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

        var sourceValidation = ValidateSources(batch.Events.Span, cancellationToken);
        if (sourceValidation is not null)
        {
            return sourceValidation;
        }

        return Options.ExecutionMode switch
        {
            RadarProcessingExecutionMode.Sequential => ProcessSequential(batch, cancellationToken),
            RadarProcessingExecutionMode.PartitionedBarrier => ProcessPartitionedBarrier(batch, cancellationToken),
            _ => throw new InvalidOperationException("Unsupported processing execution mode.")
        };
    }

    public RadarSourceProcessingSnapshot GetSourceSnapshot(int sourceId) =>
        stateStore.GetSnapshot(sourceId);

    public RadarSourceProcessingSnapshot[] CreateSourceSnapshots() =>
        stateStore.CreateSnapshots();

    public RadarSourceProcessingHandlerSnapshot GetSourceHandlerSnapshot(int sourceId) =>
        stateStore.GetHandlerSnapshot(sourceId);

    public RadarSourceProcessingHandlerSnapshot[] CreateSourceHandlerSnapshots() =>
        stateStore.CreateHandlerSnapshots();

    public RadarProcessingMetrics CreateMetrics() =>
        stateStore.CreateMetrics(processedBatchCount);

    public RadarProcessingPartitionStateSnapshot CapturePartitionState(
        RadarProcessingPartitionAssignment partition) =>
        RadarProcessingPartitionStateSnapshot.Capture(partition, stateStore);

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

    private RadarProcessingResult Valid(RadarProcessingTelemetry? telemetry = null)
    {
        var metrics = CreateMetrics();
        return new RadarProcessingResult(
            Options.ExecutionMode,
            Options.PartitionCount,
            Options.ShardCount,
            metrics,
            RadarProcessingValidationResult.Valid(metrics),
            telemetry,
            Topology.Version);
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
