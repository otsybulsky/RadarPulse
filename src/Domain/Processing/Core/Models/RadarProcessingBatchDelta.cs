using System.Buffers;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

/// <summary>
/// Holds the pooled per-source changes produced by processing one routed radar event batch.
/// </summary>
/// <remarks>
/// Instances rent backing arrays and must be disposed after the delta is committed or abandoned.
/// The delta is tied to the topology version captured by <see cref="Route"/>.
/// </remarks>
public sealed class RadarProcessingBatchDelta : IDisposable
{
    private readonly int sourceCount;
    private long[] eventCounts;
    private long[] payloadValueCounts;
    private long[] rawValueChecksums;
    private long[] firstMessageTimestampUtcTicks;
    private long[] lastMessageTimestampUtcTicks;
    private int[] touchedSourceIds;
    private int touchedSourceCount;
    private bool disposed;

    private RadarProcessingBatchDelta(
        RadarEventBatch batch,
        RadarProcessingBatchRoute route,
        int sourceCount,
        long[] eventCounts,
        long[] payloadValueCounts,
        long[] rawValueChecksums,
        long[] firstMessageTimestampUtcTicks,
        long[] lastMessageTimestampUtcTicks,
        int[] touchedSourceIds,
        int touchedSourceCount)
    {
        Batch = batch;
        Route = route;
        this.sourceCount = sourceCount;
        this.eventCounts = eventCounts;
        this.payloadValueCounts = payloadValueCounts;
        this.rawValueChecksums = rawValueChecksums;
        this.firstMessageTimestampUtcTicks = firstMessageTimestampUtcTicks;
        this.lastMessageTimestampUtcTicks = lastMessageTimestampUtcTicks;
        this.touchedSourceIds = touchedSourceIds;
        this.touchedSourceCount = touchedSourceCount;
    }

    /// <summary>
    /// Gets the batch whose routed events were used to compute this delta.
    /// </summary>
    public RadarEventBatch Batch { get; private set; }

    /// <summary>
    /// Gets the route that defines partition, shard, and topology ownership for the delta.
    /// </summary>
    public RadarProcessingBatchRoute Route { get; private set; }

    /// <summary>
    /// Gets the number of sources that received at least one event in this delta.
    /// </summary>
    public int TouchedSourceCount => touchedSourceCount;

    /// <summary>
    /// Gets the number of sources in the source universe used by the delta.
    /// </summary>
    public int SourceCount => sourceCount;

    /// <summary>
    /// Gets the source ids touched by the delta.
    /// </summary>
    public ReadOnlySpan<int> TouchedSourceIds => touchedSourceIds.AsSpan(0, TouchedSourceCount);

    internal long GetEventCount(int sourceId) => eventCounts[sourceId];

    internal long GetPayloadValueCount(int sourceId) => payloadValueCounts[sourceId];

    internal long GetRawValueChecksum(int sourceId) => rawValueChecksums[sourceId];

    internal long GetFirstMessageTimestampUtcTicks(int sourceId) => firstMessageTimestampUtcTicks[sourceId];

    internal long GetLastMessageTimestampUtcTicks(int sourceId) => lastMessageTimestampUtcTicks[sourceId];

    internal static RadarProcessingBatchDelta Create(
        RadarEventBatch batch,
        RadarProcessingBatchRoute route,
        int sourceCount)
    {
        var delta = CreateEmpty(batch, route, sourceCount);
        try
        {
            delta.ApplyAll();
            return delta;
        }
        catch
        {
            delta.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates an empty delta for a routed batch so shard work can apply events incrementally.
    /// </summary>
    public static RadarProcessingBatchDelta CreateEmpty(
        RadarEventBatch batch,
        RadarProcessingBatchRoute route,
        int sourceCount)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceCount);

        var eventCounts = RentAndClearLong(sourceCount);
        var payloadValueCounts = RentAndClearLong(sourceCount);
        var rawValueChecksums = RentAndClearLong(sourceCount);
        var firstMessageTimestampUtcTicks = RentAndClearLong(sourceCount);
        var lastMessageTimestampUtcTicks = RentAndClearLong(sourceCount);
        var touchedSourceIds = ArrayPool<int>.Shared.Rent(sourceCount);

        return new RadarProcessingBatchDelta(
            batch,
            route,
            sourceCount,
            eventCounts,
            payloadValueCounts,
            rawValueChecksums,
            firstMessageTimestampUtcTicks,
            lastMessageTimestampUtcTicks,
            touchedSourceIds,
            touchedSourceCount: 0);
    }

    /// <summary>
    /// Applies one async shard work item to this delta.
    /// </summary>
    public RadarProcessingAsyncWorkCompletion ApplyShardWorkItem(
        RadarProcessingAsyncWorkItem workItem,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        if (workItem.TopologyVersion != Route.TopologyVersion)
        {
            throw new ArgumentException("Delta work item topology version must match the route.", nameof(workItem));
        }

        var shard = Route.GetShard(workItem.ShardId);
        var eventIndexes = shard.EventIndexes.Span;
        var processedStreamEventCount = 0L;
        var processedPayloadValueCount = 0L;
        for (var i = 0; i < eventIndexes.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var eventIndex = eventIndexes[i];
            ApplyRoutedEvent(Route.GetRoutedEvent(eventIndex));
            processedStreamEventCount++;
            processedPayloadValueCount = checked(
                processedPayloadValueCount + Route.GetRoutedEvent(eventIndex).PayloadMetrics.PayloadValueCount);
        }

        return RadarProcessingAsyncWorkCompletion.Succeeded(
            workItem,
            processedStreamEventCount: processedStreamEventCount,
            processedPayloadValueCount: processedPayloadValueCount);
    }

    private void ApplyAll()
    {
        var routedEvents = Route.RoutedEvents.Span;
        for (var i = 0; i < routedEvents.Length; i++)
        {
            ApplyRoutedEvent(routedEvents[i]);
        }
    }

    private void ApplyRoutedEvent(
        RadarProcessingRoutedEvent routed)
    {
        var streamEvent = Batch.Events.Span[routed.EventIndex];
        var sourceId = routed.SourceId;
        var existingEventCount = eventCounts[sourceId];
        if (existingEventCount == 0)
        {
            var touchedIndex = Interlocked.Increment(ref touchedSourceCount) - 1;
            touchedSourceIds[touchedIndex] = sourceId;
            firstMessageTimestampUtcTicks[sourceId] = streamEvent.MessageTimestampUtcTicks;
            lastMessageTimestampUtcTicks[sourceId] = streamEvent.MessageTimestampUtcTicks;
        }
        else if (streamEvent.MessageTimestampUtcTicks < lastMessageTimestampUtcTicks[sourceId])
        {
            throw new RadarProcessingBatchDeltaValidationException(
                RadarProcessingValidationError.SourceOrderViolation,
                sourceId,
                routed.EventIndex,
                "Source-local events must be applied by non-decreasing message timestamp.");
        }

        eventCounts[sourceId] = checked(existingEventCount + 1);
        payloadValueCounts[sourceId] = checked(
            payloadValueCounts[sourceId] + routed.PayloadMetrics.PayloadValueCount);
        rawValueChecksums[sourceId] = checked(
            rawValueChecksums[sourceId] + routed.PayloadMetrics.RawValueChecksum);
        lastMessageTimestampUtcTicks[sourceId] = streamEvent.MessageTimestampUtcTicks;
    }

    private void Return()
    {
        ReturnArrays(
                sourceCount,
                eventCounts,
                payloadValueCounts,
                rawValueChecksums,
                firstMessageTimestampUtcTicks,
                lastMessageTimestampUtcTicks,
                touchedSourceIds);
    }

    /// <summary>
    /// Returns rented arrays and clears references so the delta cannot retain batch payloads.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Return();
        eventCounts = [];
        payloadValueCounts = [];
        rawValueChecksums = [];
        firstMessageTimestampUtcTicks = [];
        lastMessageTimestampUtcTicks = [];
        touchedSourceIds = [];
        touchedSourceCount = 0;
        Batch = null!;
        Route = null!;
    }

    private static long[] RentAndClearLong(int minimumLength)
    {
        var result = ArrayPool<long>.Shared.Rent(minimumLength);
        Array.Clear(result, 0, minimumLength);
        return result;
    }

    private static void ReturnArrays(
        int sourceCount,
        long[] eventCounts,
        long[] payloadValueCounts,
        long[] rawValueChecksums,
        long[] firstMessageTimestampUtcTicks,
        long[] lastMessageTimestampUtcTicks,
        int[] touchedSourceIds)
    {
        Array.Clear(eventCounts, 0, sourceCount);
        Array.Clear(payloadValueCounts, 0, sourceCount);
        Array.Clear(rawValueChecksums, 0, sourceCount);
        Array.Clear(firstMessageTimestampUtcTicks, 0, sourceCount);
        Array.Clear(lastMessageTimestampUtcTicks, 0, sourceCount);
        Array.Clear(touchedSourceIds, 0, sourceCount);
        ArrayPool<long>.Shared.Return(eventCounts);
        ArrayPool<long>.Shared.Return(payloadValueCounts);
        ArrayPool<long>.Shared.Return(rawValueChecksums);
        ArrayPool<long>.Shared.Return(firstMessageTimestampUtcTicks);
        ArrayPool<long>.Shared.Return(lastMessageTimestampUtcTicks);
        ArrayPool<int>.Shared.Return(touchedSourceIds);
    }
}
