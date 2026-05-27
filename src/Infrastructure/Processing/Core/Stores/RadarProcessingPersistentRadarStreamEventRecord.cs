using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Persistable representation of one <see cref="RadarStreamEvent"/>.
/// </summary>
/// <remarks>
/// The record mirrors the domain event layout and reuses domain validation in
/// the constructor so persisted data cannot carry an event shape the runtime
/// processing core would reject.
/// </remarks>
public sealed class RadarProcessingPersistentRadarStreamEventRecord
{
    /// <summary>
    /// Creates a persistent stream event record from the serialized event fields.
    /// </summary>
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

    /// <summary>
    /// Dense source id resolved against the source universe.
    /// </summary>
    public int SourceId { get; }

    /// <summary>
    /// Radar ordinal within the source universe.
    /// </summary>
    public ushort RadarOrdinal { get; }

    /// <summary>
    /// Volume timestamp stored as UTC ticks.
    /// </summary>
    public long VolumeTimestampUtcTicks { get; }

    /// <summary>
    /// Message timestamp stored as UTC ticks.
    /// </summary>
    public long MessageTimestampUtcTicks { get; }

    /// <summary>
    /// Source file or record ordinal from the input stream.
    /// </summary>
    public int SourceRecord { get; }

    /// <summary>
    /// Source message ordinal within the source record.
    /// </summary>
    public int SourceMessage { get; }

    /// <summary>
    /// Radial sequence number reported by the source data.
    /// </summary>
    public int RadialSequence { get; }

    /// <summary>
    /// Elevation slot component of the dense stream identity.
    /// </summary>
    public ushort ElevationSlot { get; }

    /// <summary>
    /// Azimuth bucket component of the dense stream identity.
    /// </summary>
    public ushort AzimuthBucket { get; }

    /// <summary>
    /// Range band component of the dense stream identity.
    /// </summary>
    public ushort RangeBand { get; }

    /// <summary>
    /// Moment id component of the dense stream identity.
    /// </summary>
    public ushort MomentId { get; }

    /// <summary>
    /// First gate represented by this event payload.
    /// </summary>
    public ushort GateStart { get; }

    /// <summary>
    /// Number of gates represented by this event payload.
    /// </summary>
    public ushort GateCount { get; }

    /// <summary>
    /// Encoded radar payload word size.
    /// </summary>
    public RadarStreamWordSize WordSize { get; }

    /// <summary>
    /// Scale factor used to decode payload values.
    /// </summary>
    public float Scale { get; }

    /// <summary>
    /// Offset used to decode payload values.
    /// </summary>
    public float Offset { get; }

    /// <summary>
    /// Status model used to interpret per-gate payload values.
    /// </summary>
    public RadarStreamStatusModel StatusModel { get; }

    /// <summary>
    /// Offset of this event payload in the serialized batch payload buffer.
    /// </summary>
    public int PayloadOffset { get; }

    /// <summary>
    /// Length of this event payload in the serialized batch payload buffer.
    /// </summary>
    public int PayloadLength { get; }

    /// <summary>
    /// Rehydrates the domain stream event represented by this persistent record.
    /// </summary>
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

    /// <summary>
    /// Creates a persistent record from a validated domain stream event.
    /// </summary>
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
