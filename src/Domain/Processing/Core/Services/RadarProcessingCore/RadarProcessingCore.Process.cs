using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public sealed partial class RadarProcessingCore
{
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
}
