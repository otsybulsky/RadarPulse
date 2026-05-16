using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingCore
{
    private readonly RadarSourceUniverse sourceUniverse;
    private readonly RadarSourceProcessingStateStore stateStore;
    private readonly RadarProcessingBatchRouter batchRouter;
    private long processedBatchCount;

    public RadarProcessingCore(
        RadarSourceUniverse sourceUniverse,
        RadarProcessingCoreOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sourceUniverse);

        options ??= RadarProcessingCoreOptions.Default;

        this.sourceUniverse = sourceUniverse;
        Options = options;
        Topology = new RadarProcessingTopology(sourceUniverse, options);
        batchRouter = new RadarProcessingBatchRouter(Topology);
        stateStore = new RadarSourceProcessingStateStore(sourceUniverse);
    }

    public RadarProcessingCoreOptions Options { get; }

    public RadarProcessingTopology Topology { get; }

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

    public RadarProcessingMetrics CreateMetrics() =>
        stateStore.CreateMetrics(processedBatchCount);

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
            var result = ApplyProcessedEvent(streamEvent, eventIndex, payloadMetrics);
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
        var route = batchRouter.Route(batch);
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
                    route.GetRoutedEvent(eventIndex).PayloadMetrics);
                if (result is not null)
                {
                    return result;
                }
            }
        }

        processedBatchCount = checked(processedBatchCount + 1);
        return Valid();
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
        RadarProcessingPayloadMetrics payloadMetrics)
    {
        try
        {
            stateStore.ApplyProcessedEvent(
                streamEvent,
                payloadMetrics.PayloadValueCount,
                payloadMetrics.RawValueChecksum);
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

    private RadarProcessingResult Valid()
    {
        var metrics = CreateMetrics();
        return new RadarProcessingResult(
            Options.ExecutionMode,
            Options.PartitionCount,
            Options.ShardCount,
            metrics,
            RadarProcessingValidationResult.Valid(metrics));
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
                metrics));
    }
}
