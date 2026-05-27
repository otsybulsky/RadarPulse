using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

/// <summary>
/// Source-local processing state and handler state store for one processing core.
/// </summary>
/// <remarks>
/// The store tracks source activity, processing counts, checksums, source-local
/// ordering, and optional handler slots. Handler state is stored per source using
/// the slot layout built from configured handlers.
/// </remarks>
public sealed class RadarSourceProcessingStateStore
{
    private readonly RadarSourceProcessingHandlerSlotLayout handlerSlotLayout;
    private readonly bool[] activeSources;
    private readonly long[] processedEventCounts;
    private readonly long[] processedPayloadValueCounts;
    private readonly long[] rawValueChecksums;
    private readonly long[] lastMessageTimestampUtcTicks;
    private readonly ulong[] processingChecksums;
    private readonly long[] handlerInt64Slots;
    private readonly double[] handlerDoubleSlots;
    private long activeSourceCount;

    /// <summary>
    /// Creates state store for a source universe and optional source handlers.
    /// </summary>
    public RadarSourceProcessingStateStore(
        RadarSourceUniverse sourceUniverse,
        IReadOnlyList<IRadarSourceProcessingHandler>? handlers = null)
        : this(
            sourceUniverse,
            new RadarSourceProcessingHandlerSlotLayout(handlers))
    {
    }

    internal RadarSourceProcessingStateStore(
        RadarSourceUniverse sourceUniverse,
        RadarSourceProcessingHandlerSlotLayout handlerSlotLayout)
    {
        ArgumentNullException.ThrowIfNull(sourceUniverse);
        ArgumentNullException.ThrowIfNull(handlerSlotLayout);

        SourceUniverseVersion = sourceUniverse.Version;
        SourceCount = sourceUniverse.SourceCount;
        this.handlerSlotLayout = handlerSlotLayout;

        activeSources = new bool[SourceCount];
        processedEventCounts = new long[SourceCount];
        processedPayloadValueCounts = new long[SourceCount];
        rawValueChecksums = new long[SourceCount];
        lastMessageTimestampUtcTicks = new long[SourceCount];
        processingChecksums = new ulong[SourceCount];
        handlerInt64Slots = CreateInt64HandlerSlots(SourceCount, handlerSlotLayout.TotalInt64SlotCount);
        handlerDoubleSlots = CreateDoubleHandlerSlots(SourceCount, handlerSlotLayout.TotalDoubleSlotCount);
    }

    /// <summary>
    /// Source universe version backing the state store.
    /// </summary>
    public SourceUniverseVersion SourceUniverseVersion { get; }

    /// <summary>
    /// Number of sources tracked by the store.
    /// </summary>
    public int SourceCount { get; }

    /// <summary>
    /// Number of sources that have received at least one event.
    /// </summary>
    public long ActiveSourceCount => Volatile.Read(ref activeSourceCount);

    /// <summary>
    /// Handler slot layout used by the store.
    /// </summary>
    public RadarSourceProcessingHandlerSlotLayout HandlerSlotLayout => handlerSlotLayout;

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

    /// <summary>
    /// Returns the processing snapshot for one source.
    /// </summary>
    public RadarSourceProcessingSnapshot GetSnapshot(int sourceId)
    {
        EnsureSourceId(sourceId);

        return new RadarSourceProcessingSnapshot(
            sourceId,
            activeSources[sourceId],
            processedEventCounts[sourceId],
            processedPayloadValueCounts[sourceId],
            rawValueChecksums[sourceId],
            lastMessageTimestampUtcTicks[sourceId],
            processingChecksums[sourceId]);
    }

    /// <summary>
    /// Creates processing snapshots for all sources in source-id order.
    /// </summary>
    public RadarSourceProcessingSnapshot[] CreateSnapshots()
    {
        var snapshots = new RadarSourceProcessingSnapshot[SourceCount];
        for (var sourceId = 0; sourceId < snapshots.Length; sourceId++)
        {
            snapshots[sourceId] = GetSnapshot(sourceId);
        }

        return snapshots;
    }

    /// <summary>
    /// Returns exported handler values for one source.
    /// </summary>
    public RadarSourceProcessingHandlerSnapshot GetHandlerSnapshot(int sourceId)
    {
        EnsureSourceId(sourceId);

        var values = new RadarSourceProcessingSnapshotValue[handlerSlotLayout.SnapshotFieldCount];
        var valueIndex = 0;
        foreach (var assignment in handlerSlotLayout.Assignments)
        {
            foreach (var field in assignment.Descriptor.SnapshotFields)
            {
                values[valueIndex++] = field.Type switch
                {
                    RadarSourceProcessingSnapshotFieldType.Int64 =>
                        RadarSourceProcessingSnapshotValue.FromInt64(
                            field.Name,
                            ReadInt64HandlerSlot(sourceId, assignment.Int64SlotOffset + field.SlotIndex)),
                    RadarSourceProcessingSnapshotFieldType.Double =>
                        RadarSourceProcessingSnapshotValue.FromDouble(
                            field.Name,
                            ReadDoubleHandlerSlot(sourceId, assignment.DoubleSlotOffset + field.SlotIndex)),
                    _ => throw new InvalidOperationException("Unsupported handler snapshot field type.")
                };
            }
        }

        return new RadarSourceProcessingHandlerSnapshot(sourceId, values);
    }

    /// <summary>
    /// Creates handler snapshots for all sources in source-id order.
    /// </summary>
    public RadarSourceProcessingHandlerSnapshot[] CreateHandlerSnapshots()
    {
        var snapshots = new RadarSourceProcessingHandlerSnapshot[SourceCount];
        for (var sourceId = 0; sourceId < snapshots.Length; sourceId++)
        {
            snapshots[sourceId] = GetHandlerSnapshot(sourceId);
        }

        return snapshots;
    }

    /// <summary>
    /// Creates aggregate processing metrics from current source state.
    /// </summary>
    public RadarProcessingMetrics CreateMetrics(long processedBatchCount = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(processedBatchCount);

        var processedStreamEventCount = 0L;
        var processedPayloadValueCount = 0L;
        var rawValueChecksum = 0L;
        var processingChecksum = ActiveSourceCount == 0
            ? 0UL
            : RadarStreamChecksum.Initial;

        for (var sourceId = 0; sourceId < SourceCount; sourceId++)
        {
            if (!activeSources[sourceId])
            {
                continue;
            }

            processedStreamEventCount = checked(
                processedStreamEventCount + processedEventCounts[sourceId]);
            processedPayloadValueCount = checked(
                processedPayloadValueCount + processedPayloadValueCounts[sourceId]);
            rawValueChecksum = checked(rawValueChecksum + rawValueChecksums[sourceId]);
            processingChecksum = RadarSourceProcessingChecksum.AppendSource(
                processingChecksum,
                sourceId,
                processedEventCounts[sourceId],
                processedPayloadValueCounts[sourceId],
                rawValueChecksums[sourceId],
                lastMessageTimestampUtcTicks[sourceId],
                processingChecksums[sourceId]);
        }

        return new RadarProcessingMetrics(
            processedBatchCount,
            processedStreamEventCount,
            processedPayloadValueCount,
            ActiveSourceCount,
            rawValueChecksum,
            processingChecksum);
    }

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

    private void EnsureSourceId(int sourceId)
    {
        if ((uint)sourceId < (uint)SourceCount)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(sourceId));
    }

    private static int FindFirstEventIndex(
        RadarProcessingBatchDelta delta,
        int sourceId)
    {
        var routedEvents = delta.Route.RoutedEvents.Span;
        for (var i = 0; i < routedEvents.Length; i++)
        {
            if (routedEvents[i].SourceId == sourceId)
            {
                return routedEvents[i].EventIndex;
            }
        }

        return -1;
    }

    private RadarProcessingResult CreateInvalidResult(
        RadarProcessingCoreOptions options,
        RadarProcessingTopologyVersion topologyVersion,
        long processedBatchCount,
        RadarProcessingValidationError error,
        int sourceId,
        int eventIndex,
        string message)
    {
        var metrics = CreateMetrics(processedBatchCount);
        return new RadarProcessingResult(
            options.ExecutionMode,
            options.PartitionCount,
            options.ShardCount,
            metrics,
            RadarProcessingValidationResult.Invalid(
                error,
                sourceId,
                eventIndex,
                message,
                metrics),
            topologyVersion: topologyVersion);
    }

    private void ApplyHandlers(
        int sourceId,
        in RadarStreamEvent streamEvent,
        ReadOnlySpan<byte> eventPayload,
        RadarProcessingPayloadMetrics payloadMetrics)
    {
        if (!handlerSlotLayout.HasHandlers)
        {
            return;
        }

        var context = new RadarSourceProcessingHandlerContext(
            streamEvent,
            eventPayload,
            payloadMetrics);
        foreach (var assignment in handlerSlotLayout.Assignments)
        {
            assignment.Handler.Process(
                context,
                CreateHandlerState(sourceId, assignment));
        }
    }

    private RadarSourceProcessingState CreateHandlerState(
        int sourceId,
        RadarSourceProcessingHandlerSlotAssignment assignment)
    {
        var int64Slots = assignment.Descriptor.Int64SlotCount == 0
            ? Span<long>.Empty
            : handlerInt64Slots.AsSpan(
                GetSourceSlotOffset(sourceId, handlerSlotLayout.TotalInt64SlotCount, assignment.Int64SlotOffset),
                assignment.Descriptor.Int64SlotCount);
        var doubleSlots = assignment.Descriptor.DoubleSlotCount == 0
            ? Span<double>.Empty
            : handlerDoubleSlots.AsSpan(
                GetSourceSlotOffset(sourceId, handlerSlotLayout.TotalDoubleSlotCount, assignment.DoubleSlotOffset),
                assignment.Descriptor.DoubleSlotCount);

        return new RadarSourceProcessingState(int64Slots, doubleSlots);
    }

    private void ApplyMergedHandlerValue(
        RadarProcessingHandlerDeltaValue value)
    {
        foreach (var assignment in handlerSlotLayout.Assignments)
        {
            foreach (var field in assignment.Descriptor.SnapshotFields)
            {
                if (!string.Equals(field.Name, value.FieldName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (field.Type != value.Type)
                {
                    throw new ArgumentException(
                        "Merged handler value type must match the handler descriptor field type.",
                        nameof(value));
                }

                switch (value.Type)
                {
                    case RadarSourceProcessingSnapshotFieldType.Int64:
                        handlerInt64Slots[
                            GetSourceSlotOffset(
                                value.SourceId,
                                handlerSlotLayout.TotalInt64SlotCount,
                                assignment.Int64SlotOffset + field.SlotIndex)] = value.Int64Value;
                        return;

                    case RadarSourceProcessingSnapshotFieldType.Double:
                        handlerDoubleSlots[
                            GetSourceSlotOffset(
                                value.SourceId,
                                handlerSlotLayout.TotalDoubleSlotCount,
                                assignment.DoubleSlotOffset + field.SlotIndex)] = value.DoubleValue;
                        return;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(value));
                }
            }
        }

        throw new ArgumentException(
            $"Merged handler value field '{value.FieldName}' does not match any handler descriptor field.",
            nameof(value));
    }

    private long ReadInt64HandlerSlot(
        int sourceId,
        int slotIndex) =>
        handlerInt64Slots[GetSourceSlotOffset(sourceId, handlerSlotLayout.TotalInt64SlotCount, slotIndex)];

    private double ReadDoubleHandlerSlot(
        int sourceId,
        int slotIndex) =>
        handlerDoubleSlots[GetSourceSlotOffset(sourceId, handlerSlotLayout.TotalDoubleSlotCount, slotIndex)];

    private static int GetSourceSlotOffset(
        int sourceId,
        int sourceSlotCount,
        int slotOffset) =>
        checked((sourceId * sourceSlotCount) + slotOffset);

    private static long[] CreateInt64HandlerSlots(
        int sourceCount,
        int sourceSlotCount) =>
        sourceSlotCount == 0
            ? Array.Empty<long>()
            : new long[checked(sourceCount * sourceSlotCount)];

    private static double[] CreateDoubleHandlerSlots(
        int sourceCount,
        int sourceSlotCount) =>
        sourceSlotCount == 0
            ? Array.Empty<double>()
            : new double[checked(sourceCount * sourceSlotCount)];
}
