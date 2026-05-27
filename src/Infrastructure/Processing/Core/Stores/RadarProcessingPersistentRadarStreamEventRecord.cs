using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingPersistentRadarStreamEventRecord
{
    public RadarProcessingPersistentRadarStreamEventRecord(
        int sourceId,
        ushort radarOrdinal,
        long volumeTimestampUtcTicks,
        long messageTimestampUtcTicks,
        int sourceRecord,
        int sourceMessage,
        int radialSequence,
        ushort elevationSlot,
        ushort azimuthBucket,
        ushort rangeBand,
        ushort momentId,
        ushort gateStart,
        ushort gateCount,
        RadarStreamWordSize wordSize,
        float scale,
        float offset,
        RadarStreamStatusModel statusModel,
        int payloadOffset,
        int payloadLength)
    {
        // Reuse the domain constructor validation so the persisted shape cannot
        // carry an event that the runtime would reject.
        _ = new RadarStreamEvent(
            sourceId,
            radarOrdinal,
            volumeTimestampUtcTicks,
            messageTimestampUtcTicks,
            sourceRecord,
            sourceMessage,
            radialSequence,
            elevationSlot,
            azimuthBucket,
            rangeBand,
            momentId,
            gateStart,
            gateCount,
            wordSize,
            scale,
            offset,
            statusModel,
            payloadOffset,
            payloadLength);

        SourceId = sourceId;
        RadarOrdinal = radarOrdinal;
        VolumeTimestampUtcTicks = volumeTimestampUtcTicks;
        MessageTimestampUtcTicks = messageTimestampUtcTicks;
        SourceRecord = sourceRecord;
        SourceMessage = sourceMessage;
        RadialSequence = radialSequence;
        ElevationSlot = elevationSlot;
        AzimuthBucket = azimuthBucket;
        RangeBand = rangeBand;
        MomentId = momentId;
        GateStart = gateStart;
        GateCount = gateCount;
        WordSize = wordSize;
        Scale = scale;
        Offset = offset;
        StatusModel = statusModel;
        PayloadOffset = payloadOffset;
        PayloadLength = payloadLength;
    }

    public int SourceId { get; }

    public ushort RadarOrdinal { get; }

    public long VolumeTimestampUtcTicks { get; }

    public long MessageTimestampUtcTicks { get; }

    public int SourceRecord { get; }

    public int SourceMessage { get; }

    public int RadialSequence { get; }

    public ushort ElevationSlot { get; }

    public ushort AzimuthBucket { get; }

    public ushort RangeBand { get; }

    public ushort MomentId { get; }

    public ushort GateStart { get; }

    public ushort GateCount { get; }

    public RadarStreamWordSize WordSize { get; }

    public float Scale { get; }

    public float Offset { get; }

    public RadarStreamStatusModel StatusModel { get; }

    public int PayloadOffset { get; }

    public int PayloadLength { get; }

    public RadarStreamEvent ToEvent() =>
        new(
            SourceId,
            RadarOrdinal,
            VolumeTimestampUtcTicks,
            MessageTimestampUtcTicks,
            SourceRecord,
            SourceMessage,
            RadialSequence,
            ElevationSlot,
            AzimuthBucket,
            RangeBand,
            MomentId,
            GateStart,
            GateCount,
            WordSize,
            Scale,
            Offset,
            StatusModel,
            PayloadOffset,
            PayloadLength);

    public static RadarProcessingPersistentRadarStreamEventRecord From(
        RadarStreamEvent streamEvent) =>
        new(
            streamEvent.SourceId,
            streamEvent.RadarOrdinal,
            streamEvent.VolumeTimestampUtcTicks,
            streamEvent.MessageTimestampUtcTicks,
            streamEvent.SourceRecord,
            streamEvent.SourceMessage,
            streamEvent.RadialSequence,
            streamEvent.ElevationSlot,
            streamEvent.AzimuthBucket,
            streamEvent.RangeBand,
            streamEvent.MomentId,
            streamEvent.GateStart,
            streamEvent.GateCount,
            streamEvent.WordSize,
            streamEvent.Scale,
            streamEvent.Offset,
            streamEvent.StatusModel,
            streamEvent.PayloadOffset,
            streamEvent.PayloadLength);
}
