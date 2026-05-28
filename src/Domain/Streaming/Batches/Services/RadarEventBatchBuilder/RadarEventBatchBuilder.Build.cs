namespace RadarPulse.Domain.Streaming;

public sealed partial class RadarEventBatchBuilder
{
    /// <summary>
    /// Builds an owned batch by copying the staged events and payload bytes into stable arrays.
    /// </summary>
    public RadarEventBatch Build()
    {
        var eventArray = eventCount == 0
            ? Array.Empty<RadarStreamEvent>()
            : eventBuffer.AsSpan(0, eventCount).ToArray();
        var payloadArray = payloadLength == 0
            ? Array.Empty<byte>()
            : payloadBuffer.AsSpan(0, payloadLength).ToArray();

        return new RadarEventBatch(
            streamSchemaVersion,
            dictionaryVersion,
            sourceUniverseVersion,
            eventArray,
            payloadArray,
            payloadValueCount,
            rawValueChecksum);
    }

    /// <summary>
    /// Builds a batch from the current buffers, then detaches and resets the builder buffers.
    /// </summary>
    /// <remarks>
    /// The returned batch references the buffers that were current before reset, while the builder starts over
    /// with empty replacement buffers.
    /// </remarks>
    public RadarEventBatch BuildAndReset()
    {
        var eventMemory = eventCount == 0
            ? ReadOnlyMemory<RadarStreamEvent>.Empty
            : eventBuffer.AsMemory(0, eventCount);
        var payloadMemory = payloadLength == 0
            ? ReadOnlyMemory<byte>.Empty
            : payloadBuffer.AsMemory(0, payloadLength);

        var batch = new RadarEventBatch(
            streamSchemaVersion,
            dictionaryVersion,
            sourceUniverseVersion,
            eventMemory,
            payloadMemory,
            payloadValueCount,
            rawValueChecksum);

        Reset();
        return batch;
    }

    /// <summary>
    /// Provides a leased batch to a consumer and resets the builder after the consumer returns.
    /// </summary>
    /// <remarks>
    /// The leased batch references the builder buffers, so it must not be retained beyond the callback unless it is
    /// converted with <see cref="RadarEventBatch.ToOwnedSnapshot"/>.
    /// </remarks>
    public void ConsumeLeased(Action<RadarEventBatch> consumer)
    {
        ArgumentNullException.ThrowIfNull(consumer);

        var batch = BuildLeased();
        try
        {
            consumer(batch);
        }
        finally
        {
            ResetRetainingCapacity();
        }
    }

    private RadarEventBatch BuildLeased()
    {
        var eventMemory = eventCount == 0
            ? ReadOnlyMemory<RadarStreamEvent>.Empty
            : eventBuffer.AsMemory(0, eventCount);
        var payloadMemory = payloadLength == 0
            ? ReadOnlyMemory<byte>.Empty
            : payloadBuffer.AsMemory(0, payloadLength);

        return new RadarEventBatch(
            streamSchemaVersion,
            dictionaryVersion,
            sourceUniverseVersion,
            eventMemory,
            payloadMemory,
            payloadValueCount,
            rawValueChecksum,
            RadarEventBatchLifetime.Leased);
    }
}
