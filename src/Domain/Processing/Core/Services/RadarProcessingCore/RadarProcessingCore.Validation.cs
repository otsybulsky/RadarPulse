using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public sealed partial class RadarProcessingCore
{
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
