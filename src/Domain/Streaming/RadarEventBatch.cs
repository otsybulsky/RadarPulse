namespace RadarPulse.Domain.Streaming;

public sealed class RadarEventBatch
{
    public RadarEventBatch(
        StreamSchemaVersion streamSchemaVersion,
        DictionaryVersion dictionaryVersion,
        SourceUniverseVersion sourceUniverseVersion,
        ReadOnlyMemory<RadarStreamEvent> events,
        ReadOnlyMemory<byte> payload)
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

        ValidatePayloadReferences(events.Span, payload.Length);

        StreamSchemaVersion = streamSchemaVersion;
        DictionaryVersion = dictionaryVersion;
        SourceUniverseVersion = sourceUniverseVersion;
        Events = events;
        Payload = payload;
    }

    public StreamSchemaVersion StreamSchemaVersion { get; }

    public DictionaryVersion DictionaryVersion { get; }

    public SourceUniverseVersion SourceUniverseVersion { get; }

    public ReadOnlyMemory<RadarStreamEvent> Events { get; }

    public ReadOnlyMemory<byte> Payload { get; }

    public int EventCount => Events.Length;

    public int PayloadLength => Payload.Length;

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
