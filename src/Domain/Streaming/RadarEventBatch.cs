namespace RadarPulse.Domain.Streaming;

public sealed class RadarEventBatch
{
    private readonly long precomputedPayloadValueCount;
    private readonly long precomputedRawValueChecksum;
    private readonly bool hasPrecomputedPayloadMetrics;

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
            hasPrecomputedPayloadMetrics: false)
    {
    }

    internal RadarEventBatch(
        StreamSchemaVersion streamSchemaVersion,
        DictionaryVersion dictionaryVersion,
        SourceUniverseVersion sourceUniverseVersion,
        ReadOnlyMemory<RadarStreamEvent> events,
        ReadOnlyMemory<byte> payload,
        long precomputedPayloadValueCount,
        long precomputedRawValueChecksum)
        : this(
            streamSchemaVersion,
            dictionaryVersion,
            sourceUniverseVersion,
            events,
            payload,
            precomputedPayloadValueCount,
            precomputedRawValueChecksum,
            hasPrecomputedPayloadMetrics: true)
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
        bool hasPrecomputedPayloadMetrics)
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

        ValidatePayloadReferences(events.Span, payload.Length);

        StreamSchemaVersion = streamSchemaVersion;
        DictionaryVersion = dictionaryVersion;
        SourceUniverseVersion = sourceUniverseVersion;
        Events = events;
        Payload = payload;
        this.precomputedPayloadValueCount = precomputedPayloadValueCount;
        this.precomputedRawValueChecksum = precomputedRawValueChecksum;
        this.hasPrecomputedPayloadMetrics = hasPrecomputedPayloadMetrics;
    }

    public StreamSchemaVersion StreamSchemaVersion { get; }

    public DictionaryVersion DictionaryVersion { get; }

    public SourceUniverseVersion SourceUniverseVersion { get; }

    public ReadOnlyMemory<RadarStreamEvent> Events { get; }

    public ReadOnlyMemory<byte> Payload { get; }

    public int EventCount => Events.Length;

    public int PayloadLength => Payload.Length;

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
