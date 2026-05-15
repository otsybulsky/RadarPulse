namespace RadarPulse.Domain.Streaming;

public sealed class RadarEventBatchBuilder
{
    private const int DefaultEventCapacity = 256;
    private const int DefaultPayloadCapacity = 4096;

    private readonly StreamSchemaVersion streamSchemaVersion;
    private readonly List<RadarStreamEvent> events;
    private byte[] payloadBuffer;
    private int payloadLength;
    private DictionaryVersion dictionaryVersion = DictionaryVersion.Initial;
    private SourceUniverseVersion sourceUniverseVersion = SourceUniverseVersion.Initial;
    private bool hasSourceUniverseVersion;

    public RadarEventBatchBuilder(
        int initialEventCapacity = DefaultEventCapacity,
        int initialPayloadCapacity = DefaultPayloadCapacity)
        : this(StreamSchemaVersion.Current, initialEventCapacity, initialPayloadCapacity)
    {
    }

    public RadarEventBatchBuilder(
        StreamSchemaVersion streamSchemaVersion,
        int initialEventCapacity = DefaultEventCapacity,
        int initialPayloadCapacity = DefaultPayloadCapacity)
    {
        if (streamSchemaVersion.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(streamSchemaVersion));
        }

        if (initialEventCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialEventCapacity));
        }

        if (initialPayloadCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialPayloadCapacity));
        }

        this.streamSchemaVersion = streamSchemaVersion;
        events = new List<RadarStreamEvent>(initialEventCapacity);
        payloadBuffer = initialPayloadCapacity == 0 ? [] : new byte[initialPayloadCapacity];
    }

    public int EventCount => events.Count;

    public int PayloadLength => payloadLength;

    public DictionaryVersion DictionaryVersion => dictionaryVersion;

    public SourceUniverseVersion SourceUniverseVersion => sourceUniverseVersion;

    public RadarStreamEvent AddEvent(
        RadarStreamIdentity identity,
        long volumeTimestampUtcTicks,
        long messageTimestampUtcTicks,
        int sourceRecord,
        int sourceMessage,
        int radialSequence,
        ushort gateStart,
        ushort gateCount,
        RadarStreamWordSize wordSize,
        float scale,
        float offset,
        RadarStreamStatusModel statusModel,
        ReadOnlySpan<byte> payload)
    {
        EnsureValidIdentity(identity);
        EnsureExpectedPayloadLength(gateCount, wordSize, payload.Length);

        var payloadOffset = payloadLength;
        var streamEvent = new RadarStreamEvent(
            identity.SourceId,
            identity.RadarOrdinal,
            volumeTimestampUtcTicks,
            messageTimestampUtcTicks,
            sourceRecord,
            sourceMessage,
            radialSequence,
            identity.ElevationSlot,
            identity.AzimuthBucket,
            identity.RangeBand,
            identity.MomentId,
            gateStart,
            gateCount,
            wordSize,
            scale,
            offset,
            statusModel,
            payloadOffset,
            payload.Length);

        EnsureSourceUniverseVersion(identity.SourceUniverseVersion);
        EnsurePayloadCapacity(payload.Length);
        payload.CopyTo(payloadBuffer.AsSpan(payloadOffset));
        payloadLength = checked(payloadLength + payload.Length);
        events.Add(streamEvent);

        if (identity.DictionaryVersion.Value > dictionaryVersion.Value)
        {
            dictionaryVersion = identity.DictionaryVersion;
        }

        return streamEvent;
    }

    public RadarEventBatch Build()
    {
        var eventArray = events.Count == 0
            ? Array.Empty<RadarStreamEvent>()
            : events.ToArray();
        var payloadArray = payloadLength == 0
            ? Array.Empty<byte>()
            : payloadBuffer.AsSpan(0, payloadLength).ToArray();

        return new RadarEventBatch(
            streamSchemaVersion,
            dictionaryVersion,
            sourceUniverseVersion,
            eventArray,
            payloadArray);
    }

    private static void EnsureValidIdentity(RadarStreamIdentity identity)
    {
        if (identity.DictionaryVersion.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(identity));
        }

        if (identity.SourceUniverseVersion.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(identity));
        }
    }

    private void EnsureSourceUniverseVersion(SourceUniverseVersion identitySourceUniverseVersion)
    {
        if (!hasSourceUniverseVersion)
        {
            sourceUniverseVersion = identitySourceUniverseVersion;
            hasSourceUniverseVersion = true;
            return;
        }

        if (sourceUniverseVersion != identitySourceUniverseVersion)
        {
            throw new ArgumentException("All events in one batch must use the same source-universe version.");
        }
    }

    private static void EnsureExpectedPayloadLength(
        ushort gateCount,
        RadarStreamWordSize wordSize,
        int payloadLength)
    {
        ArgumentOutOfRangeException.ThrowIfZero(gateCount);

        var bytesPerGate = wordSize switch
        {
            RadarStreamWordSize.EightBit => 1,
            RadarStreamWordSize.SixteenBit => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(wordSize))
        };
        var expectedLength = checked(gateCount * bytesPerGate);
        if (payloadLength != expectedLength)
        {
            throw new ArgumentException("Payload length must match gate count and word size.", nameof(payloadLength));
        }
    }

    private void EnsurePayloadCapacity(int appendLength)
    {
        var requiredLength = checked(payloadLength + appendLength);
        if (requiredLength <= payloadBuffer.Length)
        {
            return;
        }

        var newLength = payloadBuffer.Length == 0 ? DefaultPayloadCapacity : payloadBuffer.Length;
        while (newLength < requiredLength)
        {
            newLength = checked(newLength * 2);
        }

        Array.Resize(ref payloadBuffer, newLength);
    }
}
