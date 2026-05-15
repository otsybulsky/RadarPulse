using System.Runtime.InteropServices;

namespace RadarPulse.Domain.Streaming;

[StructLayout(LayoutKind.Sequential, Size = SizeInBytes)]
public readonly struct RadarStreamEvent
{
    public const int SizeInBytes = 64;

    public readonly long VolumeTimestampUtcTicks;
    public readonly long MessageTimestampUtcTicks;
    public readonly int SourceId;
    public readonly int SourceRecord;
    public readonly int SourceMessage;
    public readonly int RadialSequence;
    public readonly int PayloadOffset;
    public readonly int PayloadLength;
    public readonly float Scale;
    public readonly float Offset;
    public readonly ushort RadarOrdinal;
    public readonly ushort MomentId;
    public readonly ushort ElevationSlot;
    public readonly ushort AzimuthBucket;
    public readonly ushort RangeBand;
    public readonly ushort GateStart;
    public readonly ushort GateCount;
    public readonly RadarStreamWordSize WordSize;
    public readonly RadarStreamStatusModel StatusModel;

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

    public int BytesPerGate => WordSize switch
    {
        RadarStreamWordSize.EightBit => 1,
        RadarStreamWordSize.SixteenBit => 2,
        _ => throw new InvalidOperationException("Unsupported radar stream word size.")
    };

    public int ExpectedPayloadLength => checked(GateCount * BytesPerGate);
}
