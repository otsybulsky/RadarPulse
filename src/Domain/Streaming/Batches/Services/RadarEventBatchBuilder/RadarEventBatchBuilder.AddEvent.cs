namespace RadarPulse.Domain.Streaming;

public sealed partial class RadarEventBatchBuilder
{
    /// <summary>
    /// Appends one radar stream event and copies its payload bytes into the batch payload buffer.
    /// </summary>
    /// <returns>The compact event descriptor stored in the batch.</returns>
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
        EnsureEventCapacity();
        EnsurePayloadCapacity(payload.Length);
        payload.CopyTo(payloadBuffer.AsSpan(payloadOffset));
        payloadLength = checked(payloadLength + payload.Length);
        payloadValueCount += gateCount;
        rawValueChecksum += SumRawValues(wordSize, payload);
        eventBuffer[eventCount] = streamEvent;
        eventCount++;

        if (identity.DictionaryVersion.Value > dictionaryVersion.Value)
        {
            dictionaryVersion = identity.DictionaryVersion;
        }

        return streamEvent;
    }
}
