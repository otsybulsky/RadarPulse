namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Immutable batch of radar stream events and the contiguous payload bytes referenced by those events.
/// </summary>
/// <remarks>
/// The batch carries stream schema, dictionary, and source-universe versions so validation can prove that
/// compact event identifiers and payload references are interpreted against the same contracts that produced them.
/// A batch can either own its backing memory or temporarily reference leased builder buffers.
/// </remarks>
public sealed class RadarEventBatch
{
    private readonly long precomputedPayloadValueCount;
    private readonly long precomputedRawValueChecksum;
    private readonly bool hasPrecomputedPayloadMetrics;

    /// <summary>
    /// Creates an owned batch from supplied event and payload memory without cached payload metrics.
    /// </summary>
    /// <summary>
    /// Creates an event batch with precomputed payload metrics.
    /// </summary>
    public RadarEventBatch(
        StreamSchemaVersion streamSchemaVersion,
        DictionaryVersion dictionaryVersion,
        SourceUniverseVersion sourceUniverseVersion,
        ReadOnlyMemory<RadarStreamEvent> events,
        ReadOnlyMemory<byte> payload)
        : this(
            streamSchemaVersion,
            dictionaryVersion,
            sourceUniverseVersion,
            events,
            payload,
            precomputedPayloadValueCount: 0,
            precomputedRawValueChecksum: 0,
            hasPrecomputedPayloadMetrics: false,
            RadarEventBatchLifetime.Owned)
    {
    }

    public RadarEventBatch(
        StreamSchemaVersion streamSchemaVersion,
        DictionaryVersion dictionaryVersion,
        SourceUniverseVersion sourceUniverseVersion,
        ReadOnlyMemory<RadarStreamEvent> events,
        ReadOnlyMemory<byte> payload,
        long precomputedPayloadValueCount,
        long precomputedRawValueChecksum,
        RadarEventBatchLifetime lifetime = RadarEventBatchLifetime.Owned)
        : this(
            streamSchemaVersion,
            dictionaryVersion,
            sourceUniverseVersion,
            events,
            payload,
            precomputedPayloadValueCount,
            precomputedRawValueChecksum,
            hasPrecomputedPayloadMetrics: true,
            lifetime)
    {
    }

    private RadarEventBatch(
        StreamSchemaVersion streamSchemaVersion,
        DictionaryVersion dictionaryVersion,
        SourceUniverseVersion sourceUniverseVersion,
        ReadOnlyMemory<RadarStreamEvent> events,
        ReadOnlyMemory<byte> payload,
        long precomputedPayloadValueCount,
        long precomputedRawValueChecksum,
        bool hasPrecomputedPayloadMetrics,
        RadarEventBatchLifetime lifetime)
    {
        if (streamSchemaVersion.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(streamSchemaVersion));
        }

        if (dictionaryVersion.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dictionaryVersion));
        }

        if (sourceUniverseVersion.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceUniverseVersion));
        }

        if (hasPrecomputedPayloadMetrics)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(precomputedPayloadValueCount);
            ArgumentOutOfRangeException.ThrowIfNegative(precomputedRawValueChecksum);
        }

        if (lifetime is not RadarEventBatchLifetime.Owned and not RadarEventBatchLifetime.Leased)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }

        ValidatePayloadReferences(events.Span, payload.Length);

        StreamSchemaVersion = streamSchemaVersion;
        DictionaryVersion = dictionaryVersion;
        SourceUniverseVersion = sourceUniverseVersion;
        Events = events;
        Payload = payload;
        Lifetime = lifetime;
        this.precomputedPayloadValueCount = precomputedPayloadValueCount;
        this.precomputedRawValueChecksum = precomputedRawValueChecksum;
        this.hasPrecomputedPayloadMetrics = hasPrecomputedPayloadMetrics;
    }

    /// <summary>
    /// Gets the stream layout schema version used by every event in the batch.
    /// </summary>
    public StreamSchemaVersion StreamSchemaVersion { get; }

    /// <summary>
    /// Gets the dictionary version required to resolve compact radar and moment identifiers in the batch.
    /// </summary>
    public DictionaryVersion DictionaryVersion { get; }

    /// <summary>
    /// Gets the source-universe version required to resolve source identifiers and source dimensions.
    /// </summary>
    public SourceUniverseVersion SourceUniverseVersion { get; }

    /// <summary>
    /// Gets the ordered stream events that reference slices of <see cref="Payload"/>.
    /// </summary>
    public ReadOnlyMemory<RadarStreamEvent> Events { get; }

    /// <summary>
    /// Gets contiguous raw payload bytes referenced by <see cref="Events"/>.
    /// </summary>
    public ReadOnlyMemory<byte> Payload { get; }

    /// <summary>
    /// Gets whether the batch owns its backing memory or temporarily references leased buffers.
    /// </summary>
    public RadarEventBatchLifetime Lifetime { get; }

    /// <summary>
    /// Gets the number of events in the batch.
    /// </summary>
    public int EventCount => Events.Length;

    /// <summary>
    /// Gets the number of payload bytes in the batch.
    /// </summary>
    public int PayloadLength => Payload.Length;

    /// <summary>
    /// Returns a batch that owns stable backing arrays and can be safely retained.
    /// </summary>
    /// <remarks>
    /// Owned batches are returned as-is. Leased batches copy their event and payload memory into new arrays.
    /// </remarks>
    public RadarEventBatch ToOwnedSnapshot()
    {
        if (Lifetime == RadarEventBatchLifetime.Owned)
        {
            return this;
        }

        var eventArray = Events.Length == 0
            ? Array.Empty<RadarStreamEvent>()
            : Events.Span.ToArray();
        var payloadArray = Payload.Length == 0
            ? Array.Empty<byte>()
            : Payload.Span.ToArray();

        return new RadarEventBatch(
            StreamSchemaVersion,
            DictionaryVersion,
            SourceUniverseVersion,
            eventArray,
            payloadArray,
            precomputedPayloadValueCount,
            precomputedRawValueChecksum,
            RadarEventBatchLifetime.Owned);
    }

    /// <summary>
    /// Attempts to read payload metrics captured when the batch was built.
    /// </summary>
    /// <param name="payloadValueCount">Receives the total number of decoded payload values when metrics are cached.</param>
    /// <param name="rawValueChecksum">Receives the raw payload checksum when metrics are cached.</param>
    /// <returns><see langword="true"/> when the batch contains cached payload metrics; otherwise <see langword="false"/>.</returns>
    public bool TryGetPayloadMetrics(out long payloadValueCount, out long rawValueChecksum)
    {
        payloadValueCount = precomputedPayloadValueCount;
        rawValueChecksum = precomputedRawValueChecksum;
        return hasPrecomputedPayloadMetrics;
    }

    private static void ValidatePayloadReferences(ReadOnlySpan<RadarStreamEvent> events, int payloadLength)
    {
        for (var i = 0; i < events.Length; i++)
        {
            var streamEvent = events[i];
            if (streamEvent.PayloadLength != streamEvent.ExpectedPayloadLength)
            {
                throw new ArgumentException("Event payload length does not match gate count and word size.", nameof(events));
            }

            if (streamEvent.PayloadOffset > payloadLength - streamEvent.PayloadLength)
            {
                throw new ArgumentOutOfRangeException(nameof(events), "Event payload reference exceeds batch payload storage.");
            }
        }
    }
}
