using System.Runtime.InteropServices;

namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Fixed-size metadata record describing one radar stream payload segment.
/// </summary>
/// <remarks>
/// The struct is intentionally sequential and 64 bytes so batches can keep event
/// metadata dense. Payload bytes are stored separately and referenced by offset
/// and length.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Size = SizeInBytes)]
public readonly struct RadarStreamEvent
{
    /// <summary>
    /// Fixed binary size of a stream event metadata record.
    /// </summary>
    public const int SizeInBytes = 64;

    /// <summary>
    /// Volume timestamp in UTC ticks.
    /// </summary>
    public readonly long VolumeTimestampUtcTicks;

    /// <summary>
    /// Message timestamp in UTC ticks used for batch chronology validation.
    /// </summary>
    public readonly long MessageTimestampUtcTicks;

    /// <summary>
    /// Dense source id from the source universe.
    /// </summary>
    public readonly int SourceId;

    /// <summary>
    /// Source record number from the upstream feed.
    /// </summary>
    public readonly int SourceRecord;

    /// <summary>
    /// Source message number from the upstream feed.
    /// </summary>
    public readonly int SourceMessage;

    /// <summary>
    /// Radial sequence number within the source data.
    /// </summary>
    public readonly int RadialSequence;

    /// <summary>
    /// Offset into the owning batch payload buffer.
    /// </summary>
    public readonly int PayloadOffset;

    /// <summary>
    /// Length of the payload segment referenced by this event.
    /// </summary>
    public readonly int PayloadLength;

    /// <summary>
    /// Scale factor for decoded gate values.
    /// </summary>
    public readonly float Scale;

    /// <summary>
    /// Offset for decoded gate values.
    /// </summary>
    public readonly float Offset;

    /// <summary>
    /// Dense radar code ordinal.
    /// </summary>
    public readonly ushort RadarOrdinal;

    /// <summary>
    /// Dense moment id.
    /// </summary>
    public readonly ushort MomentId;

    /// <summary>
    /// Elevation slot dimension.
    /// </summary>
    public readonly ushort ElevationSlot;

    /// <summary>
    /// Azimuth bucket dimension.
    /// </summary>
    public readonly ushort AzimuthBucket;

    /// <summary>
    /// Range band dimension.
    /// </summary>
    public readonly ushort RangeBand;

    /// <summary>
    /// First gate represented by the payload segment.
    /// </summary>
    public readonly ushort GateStart;

    /// <summary>
    /// Number of gates represented by the payload segment.
    /// </summary>
    public readonly ushort GateCount;

    /// <summary>
    /// Encoded gate value width.
    /// </summary>
    public readonly RadarStreamWordSize WordSize;

    /// <summary>
    /// Status model used to interpret payload values.
    /// </summary>
    public readonly RadarStreamStatusModel StatusModel;

    /// <summary>
    /// Creates a stream event metadata record.
    /// </summary>
    public RadarStreamEvent(
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
        ArgumentOutOfRangeException.ThrowIfNegative(sourceId);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceRecord);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceMessage);
        ArgumentOutOfRangeException.ThrowIfNegative(radialSequence);
        ArgumentOutOfRangeException.ThrowIfZero(gateCount);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadOffset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(payloadLength);

        if (wordSize is not RadarStreamWordSize.EightBit and not RadarStreamWordSize.SixteenBit)
        {
            throw new ArgumentOutOfRangeException(nameof(wordSize));
        }

        if (statusModel == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(statusModel));
        }

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
    /// Number of payload bytes per gate implied by word size.
    /// </summary>
    public int BytesPerGate => WordSize switch
    {
        RadarStreamWordSize.EightBit => 1,
        RadarStreamWordSize.SixteenBit => 2,
        _ => throw new InvalidOperationException("Unsupported radar stream word size.")
    };

    /// <summary>
    /// Expected payload length from gate count and word size.
    /// </summary>
    public int ExpectedPayloadLength => checked(GateCount * BytesPerGate);
}
