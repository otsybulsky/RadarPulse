namespace RadarPulse.Domain.Processing;

public sealed partial class RadarSourceProcessingStateStore
{
    internal RadarProcessingResult? ValidateDeltaForCommit(
        RadarProcessingBatchDelta delta,
        RadarProcessingCoreOptions options,
        RadarProcessingTopologyVersion topologyVersion,
        long processedBatchCount)
    {
        ArgumentNullException.ThrowIfNull(delta);
        ArgumentNullException.ThrowIfNull(options);

        foreach (var sourceId in delta.TouchedSourceIds)
        {
            if (activeSources[sourceId] &&
                delta.GetFirstMessageTimestampUtcTicks(sourceId) < lastMessageTimestampUtcTicks[sourceId])
            {
                return CreateInvalidResult(
                    options,
                    topologyVersion,
                    processedBatchCount,
                    RadarProcessingValidationError.SourceOrderViolation,
                    sourceId,
                    FindFirstEventIndex(delta, sourceId),
                    "Source-local events must be applied by non-decreasing message timestamp.");
            }
        }

        return null;
    }

    internal void ApplyDelta(
        RadarProcessingBatchDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        if (handlerSlotLayout.HasHandlers)
        {
            throw new InvalidOperationException(
                "Processing handlers require a handler-delta contract before ordered commit.");
        }

        var events = delta.Batch.Events.Span;
        var routedEvents = delta.Route.RoutedEvents.Span;
        for (var i = 0; i < routedEvents.Length; i++)
        {
            var routed = routedEvents[i];
            ApplyProcessedEvent(
                events[routed.EventIndex],
                routed.PayloadMetrics.PayloadValueCount,
                routed.PayloadMetrics.RawValueChecksum);
        }
    }

    internal void ApplyDeltaWithoutHandlers(
        RadarProcessingBatchDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);

        var events = delta.Batch.Events.Span;
        var routedEvents = delta.Route.RoutedEvents.Span;
        for (var i = 0; i < routedEvents.Length; i++)
        {
            var routed = routedEvents[i];
            ApplyProcessedEventCore(
                events[routed.EventIndex],
                ReadOnlySpan<byte>.Empty,
                routed.PayloadMetrics,
                applyHandlers: false);
        }
    }

    internal void ApplyMergedHandlerValues(
        IReadOnlyList<RadarProcessingHandlerDeltaValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (!handlerSlotLayout.HasHandlers)
        {
            if (values.Count != 0)
            {
                throw new ArgumentException(
                    "Merged handler values require a processing core with handlers.",
                    nameof(values));
            }

            return;
        }

        foreach (var value in values)
        {
            EnsureSourceId(value.SourceId);
            ApplyMergedHandlerValue(value);
        }
    }

    internal void ApplyMergedHandlerValueGroups(
        IReadOnlyList<IReadOnlyList<RadarProcessingHandlerDeltaValue>> valueGroups)
    {
        ArgumentNullException.ThrowIfNull(valueGroups);
        if (!handlerSlotLayout.HasHandlers)
        {
            if (valueGroups.Any(static group => group.Count != 0))
            {
                throw new ArgumentException(
                    "Merged handler values require a processing core with handlers.",
                    nameof(valueGroups));
            }

            return;
        }

        for (var groupIndex = 0; groupIndex < valueGroups.Count; groupIndex++)
        {
            var group = valueGroups[groupIndex];
            for (var valueIndex = 0; valueIndex < group.Count; valueIndex++)
            {
                var value = group[valueIndex];
                EnsureSourceId(value.SourceId);
                ApplyMergedHandlerValue(value);
            }
        }
    }
}
