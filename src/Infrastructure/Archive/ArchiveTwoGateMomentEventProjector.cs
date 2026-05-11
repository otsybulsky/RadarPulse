using System.Buffers.Binary;
using System.Text;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class ArchiveTwoGateMomentEventProjector : IArchiveTwoMessageConsumer
{
    private const int MessageHeaderLength = 16;
    private const int Type31DataHeaderMinimumLength = 72;
    private const int Type31DataBlockPointerOffset = 32;
    private const int Type31DataBlockPointerLength = 4;
    private const int Type31MaximumDataBlockPointers = 10;
    private const int GenericMomentDescriptorLength = 28;
    private const int GenericMomentDataOffset = 28;
    private const int GenericMomentGateCountOffset = 8;
    private const int GenericMomentFirstGateRangeOffset = 10;
    private const int GenericMomentGateSpacingOffset = 12;
    private const int GenericMomentWordSizeOffset = 19;
    private const int GenericMomentScaleOffset = 20;
    private const int GenericMomentOffsetOffset = 24;

    private readonly Action<ArchiveTwoGateMomentEvent> acceptEvent;
    private int radialSequenceNumber;
    private int currentSweepSequenceNumber;
    private int currentSweepElevationNumber;
    private int currentSweepRadialCount;

    public ArchiveTwoGateMomentEventProjector(
        string radarId,
        DateTimeOffset volumeTimestamp,
        Action<ArchiveTwoGateMomentEvent> acceptEvent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(radarId);
        RadarId = radarId;
        VolumeTimestamp = volumeTimestamp;
        this.acceptEvent = acceptEvent ?? throw new ArgumentNullException(nameof(acceptEvent));
    }

    public string RadarId { get; }

    public DateTimeOffset VolumeTimestamp { get; }

    public void Reset() => Reset(default);

    internal void Reset(ArchiveTwoGateMomentProjectorState state)
    {
        radialSequenceNumber = state.RadialSequenceNumber;
        currentSweepSequenceNumber = state.CurrentSweepSequenceNumber;
        currentSweepElevationNumber = state.CurrentSweepElevationNumber;
        currentSweepRadialCount = state.CurrentSweepRadialCount;
    }

    public void AcceptMessage(ReadOnlySpan<byte> message, ArchiveTwoMessageSource source)
    {
        if (message.Length < MessageHeaderLength || message[3] != 31)
        {
            return;
        }

        ParseType31(message[MessageHeaderLength..], source);
    }

    private void ParseType31(ReadOnlySpan<byte> payload, ArchiveTwoMessageSource source)
    {
        if (payload.Length < Type31DataHeaderMinimumLength)
        {
            return;
        }

        var radialLength = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(18, 2));
        var parseLength = Math.Min(payload.Length, radialLength > 0 ? radialLength : payload.Length);
        var parsePayload = payload[..parseLength];
        var radial = ReadType31RadialMetadata(parsePayload);
        var sweepSequenceNumber = AcceptRadial(radial);
        var sourceOrder = new ArchiveTwoRadialSourceOrder(
            source.CompressedRecordSequenceNumber,
            source.MessageSequenceNumberInRecord,
            radialSequenceNumber);
        var blockCount = BinaryPrimitives.ReadUInt16BigEndian(parsePayload.Slice(30, 2));
        var pointerCount = Math.Min(
            Math.Min((int)blockCount, Type31MaximumDataBlockPointers),
            (parsePayload.Length - Type31DataBlockPointerOffset) / Type31DataBlockPointerLength);

        for (var i = 0; i < pointerCount; i++)
        {
            var pointer = BinaryPrimitives.ReadInt32BigEndian(
                parsePayload.Slice(Type31DataBlockPointerOffset + i * Type31DataBlockPointerLength, Type31DataBlockPointerLength));
            if (pointer <= 0 || pointer >= parsePayload.Length)
            {
                continue;
            }

            var block = parsePayload[pointer..];
            if (block.Length < GenericMomentDescriptorLength || block[0] != (byte)'D')
            {
                continue;
            }

            ProjectMomentBlock(block, sweepSequenceNumber, radial.ElevationNumber, sourceOrder);
        }
    }

    private static Type31RadialMetadata ReadType31RadialMetadata(ReadOnlySpan<byte> payload) =>
        new(
            payload[21],
            payload[22]);

    private int AcceptRadial(Type31RadialMetadata radial)
    {
        var nextState = AdvanceState(
            new ArchiveTwoGateMomentProjectorState(
                radialSequenceNumber,
                currentSweepSequenceNumber,
                currentSweepElevationNumber,
                currentSweepRadialCount),
            radial.RadialStatus,
            radial.ElevationNumber,
            out var sweepSequenceNumber);
        Reset(nextState);
        return sweepSequenceNumber;
    }

    internal static ArchiveTwoGateMomentProjectorState AdvanceState(
        ArchiveTwoGateMomentProjectorState state,
        int radialStatus,
        int elevationNumber,
        out int sweepSequenceNumber)
    {
        var radialSequenceNumber = state.RadialSequenceNumber + 1;
        var currentSweepSequenceNumber = state.CurrentSweepSequenceNumber;
        var currentSweepElevationNumber = state.CurrentSweepElevationNumber;
        var currentSweepRadialCount = state.CurrentSweepRadialCount;

        if (currentSweepSequenceNumber == 0 ||
            (currentSweepRadialCount > 0 &&
                (IsStartRadialStatus(radialStatus) ||
                    elevationNumber != currentSweepElevationNumber)))
        {
            currentSweepSequenceNumber++;
            currentSweepElevationNumber = elevationNumber;
            currentSweepRadialCount = 0;
        }

        currentSweepRadialCount++;
        sweepSequenceNumber = currentSweepSequenceNumber;
        return new ArchiveTwoGateMomentProjectorState(
            radialSequenceNumber,
            currentSweepSequenceNumber,
            currentSweepElevationNumber,
            currentSweepRadialCount);
    }

    private static bool IsStartRadialStatus(int radialStatus) =>
        radialStatus is 0 or 3 or 5 or 80 or 83 or 85;

    private void ProjectMomentBlock(
        ReadOnlySpan<byte> block,
        int sweepSequenceNumber,
        int elevationNumber,
        ArchiveTwoRadialSourceOrder sourceOrder)
    {
        var momentName = ReadDataBlockName(block);
        if (momentName.Length == 0)
        {
            return;
        }

        var metadata = ReadMomentMetadata(block);
        switch (metadata.WordSizeBits)
        {
            case 8:
                ProjectEightBitMomentBlock(block[GenericMomentDataOffset..], momentName, metadata, sweepSequenceNumber, elevationNumber, sourceOrder);
                break;
            case 16:
                ProjectSixteenBitMomentBlock(block[GenericMomentDataOffset..], momentName, metadata, sweepSequenceNumber, elevationNumber, sourceOrder);
                break;
        }
    }

    private void ProjectEightBitMomentBlock(
        ReadOnlySpan<byte> data,
        string momentName,
        Type31MomentMetadata metadata,
        int sweepSequenceNumber,
        int elevationNumber,
        ArchiveTwoRadialSourceOrder sourceOrder)
    {
        if (data.Length < metadata.GateCount)
        {
            return;
        }

        for (var gateIndex = 0; gateIndex < metadata.GateCount; gateIndex++)
        {
            ProjectEvent(
                momentName,
                data[gateIndex],
                gateIndex,
                metadata,
                sweepSequenceNumber,
                elevationNumber,
                sourceOrder);
        }
    }

    private void ProjectSixteenBitMomentBlock(
        ReadOnlySpan<byte> data,
        string momentName,
        Type31MomentMetadata metadata,
        int sweepSequenceNumber,
        int elevationNumber,
        ArchiveTwoRadialSourceOrder sourceOrder)
    {
        var requiredBytes = checked(metadata.GateCount * sizeof(ushort));
        if (data.Length < requiredBytes)
        {
            return;
        }

        for (var gateIndex = 0; gateIndex < metadata.GateCount; gateIndex++)
        {
            ProjectEvent(
                momentName,
                BinaryPrimitives.ReadUInt16BigEndian(data.Slice(gateIndex * sizeof(ushort), sizeof(ushort))),
                gateIndex,
                metadata,
                sweepSequenceNumber,
                elevationNumber,
                sourceOrder);
        }
    }

    private void ProjectEvent(
        string momentName,
        int rawValue,
        int gateIndex,
        Type31MomentMetadata metadata,
        int sweepSequenceNumber,
        int elevationNumber,
        ArchiveTwoRadialSourceOrder sourceOrder)
    {
        var status = ClassifyMomentValue(momentName, rawValue, metadata);
        var calibratedValue = status == ArchiveTwoGateMomentStatus.Valid
            ? (double?)((rawValue - metadata.Offset) / metadata.Scale)
            : null;
        acceptEvent(new ArchiveTwoGateMomentEvent(
            RadarId,
            VolumeTimestamp,
            sweepSequenceNumber,
            elevationNumber,
            sourceOrder.Type31RadialSequenceNumber,
            gateIndex,
            metadata.FirstGateRangeKilometers + gateIndex * metadata.GateSpacingKilometers,
            momentName,
            rawValue,
            status,
            calibratedValue,
            sourceOrder));
    }

    private static ArchiveTwoGateMomentStatus ClassifyMomentValue(
        string momentName,
        int rawValue,
        Type31MomentMetadata metadata)
    {
        if (IsClutterFilterPowerRemovedMoment(momentName))
        {
            return rawValue switch
            {
                0 => ArchiveTwoGateMomentStatus.ClutterFilterNotApplied,
                1 => ArchiveTwoGateMomentStatus.PointClutterFilterApplied,
                2 => ArchiveTwoGateMomentStatus.DualPolarizationFiltered,
                < 8 => ArchiveTwoGateMomentStatus.Reserved,
                _ => IsCalibratable(metadata) ? ArchiveTwoGateMomentStatus.Valid : ArchiveTwoGateMomentStatus.Unsupported
            };
        }

        return rawValue switch
        {
            0 => ArchiveTwoGateMomentStatus.BelowThreshold,
            1 => ArchiveTwoGateMomentStatus.RangeFolded,
            _ => IsCalibratable(metadata) ? ArchiveTwoGateMomentStatus.Valid : ArchiveTwoGateMomentStatus.Unsupported
        };
    }

    private static bool IsCalibratable(Type31MomentMetadata metadata) =>
        metadata.Scale != 0 && float.IsFinite(metadata.Scale);

    private static bool IsClutterFilterPowerRemovedMoment(string momentName) =>
        string.Equals(momentName, "CFP", StringComparison.Ordinal);

    private static string ReadDataBlockName(ReadOnlySpan<byte> block) =>
        Encoding.ASCII.GetString(block.Slice(1, 3)).TrimEnd('\0', ' ');

    private static Type31MomentMetadata ReadMomentMetadata(ReadOnlySpan<byte> block) =>
        new(
            BinaryPrimitives.ReadUInt16BigEndian(block.Slice(GenericMomentGateCountOffset, 2)),
            BinaryPrimitives.ReadUInt16BigEndian(block.Slice(GenericMomentFirstGateRangeOffset, 2)) / 1_000f,
            BinaryPrimitives.ReadUInt16BigEndian(block.Slice(GenericMomentGateSpacingOffset, 2)) / 1_000f,
            block[GenericMomentWordSizeOffset],
            ReadSingleBigEndian(block.Slice(GenericMomentScaleOffset, 4)),
            ReadSingleBigEndian(block.Slice(GenericMomentOffsetOffset, 4)));

    private static float ReadSingleBigEndian(ReadOnlySpan<byte> buffer) =>
        BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(buffer));

    private readonly record struct Type31MomentMetadata(
        int GateCount,
        float FirstGateRangeKilometers,
        float GateSpacingKilometers,
        int WordSizeBits,
        float Scale,
        float Offset);

    private readonly record struct Type31RadialMetadata(
        int RadialStatus,
        int ElevationNumber);
}

internal readonly record struct ArchiveTwoGateMomentProjectorState(
    int RadialSequenceNumber,
    int CurrentSweepSequenceNumber,
    int CurrentSweepElevationNumber,
    int CurrentSweepRadialCount);
