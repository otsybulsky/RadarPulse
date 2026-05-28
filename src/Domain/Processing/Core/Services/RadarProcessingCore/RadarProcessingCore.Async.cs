using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public sealed partial class RadarProcessingCore
{
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
}
