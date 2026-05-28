using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public sealed partial class RadarSourceProcessingStateStore
{
    /// <summary>
    /// Applies one processed event when no payload-aware handlers are configured.
    /// </summary>
    public void ApplyProcessedEvent(
        in RadarStreamEvent streamEvent,
        long processedPayloadValueCount,
        long rawValueChecksum)
    {
        if (handlerSlotLayout.HasHandlers)
        {
            throw new InvalidOperationException(
                "Processing handlers require the payload-aware ApplyProcessedEvent overload.");
        }

        ApplyProcessedEvent(
            streamEvent,
            ReadOnlySpan<byte>.Empty,
            new RadarProcessingPayloadMetrics(
                processedPayloadValueCount,
                rawValueChecksum));
    }

    /// <summary>
    /// Applies one processed event and invokes configured handlers with payload context.
    /// </summary>
    public void ApplyProcessedEvent(
        in RadarStreamEvent streamEvent,
        ReadOnlySpan<byte> eventPayload,
        RadarProcessingPayloadMetrics payloadMetrics) =>
        ApplyProcessedEventCore(
            streamEvent,
            eventPayload,
            payloadMetrics,
            applyHandlers: true);

    private void ApplyProcessedEventCore(
        in RadarStreamEvent streamEvent,
        ReadOnlySpan<byte> eventPayload,
        RadarProcessingPayloadMetrics payloadMetrics,
        bool applyHandlers)
    {
        var sourceId = streamEvent.SourceId;
        EnsureSourceId(sourceId);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadMetrics.PayloadValueCount);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadMetrics.RawValueChecksum);

        var isActive = activeSources[sourceId];
        if (isActive &&
            streamEvent.MessageTimestampUtcTicks < lastMessageTimestampUtcTicks[sourceId])
        {
            throw new InvalidOperationException(
                "Source-local events must be applied by non-decreasing message timestamp.");
        }

        if (!isActive)
        {
            activeSources[sourceId] = true;
            Interlocked.Increment(ref activeSourceCount);
        }

        processedEventCounts[sourceId] = checked(processedEventCounts[sourceId] + 1);
        processedPayloadValueCounts[sourceId] = checked(
            processedPayloadValueCounts[sourceId] + payloadMetrics.PayloadValueCount);
        rawValueChecksums[sourceId] = checked(rawValueChecksums[sourceId] + payloadMetrics.RawValueChecksum);
        lastMessageTimestampUtcTicks[sourceId] = streamEvent.MessageTimestampUtcTicks;
        processingChecksums[sourceId] = RadarSourceProcessingChecksum.AppendEvent(
            isActive ? processingChecksums[sourceId] : RadarStreamChecksum.Initial,
            streamEvent,
            payloadMetrics.PayloadValueCount,
            payloadMetrics.RawValueChecksum);
        if (applyHandlers)
        {
            ApplyHandlers(sourceId, streamEvent, eventPayload, payloadMetrics);
        }
    }
}
