using System.Buffers.Binary;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class ArchiveTwoGateMomentEventProjector
{
    private void ProjectMomentBlock(
        ReadOnlySpan<byte> block,
        int sweepSequenceNumber,
        int elevationNumber,
        DateTimeOffset messageTimestamp,
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
                ProjectEightBitMomentBlock(block[GenericMomentDataOffset..], momentName, metadata, sweepSequenceNumber, elevationNumber, messageTimestamp, sourceOrder);
                break;
            case 16:
                ProjectSixteenBitMomentBlock(block[GenericMomentDataOffset..], momentName, metadata, sweepSequenceNumber, elevationNumber, messageTimestamp, sourceOrder);
                break;
        }
    }

    private void ProjectEightBitMomentBlock(
        ReadOnlySpan<byte> data,
        string momentName,
        Type31MomentMetadata metadata,
        int sweepSequenceNumber,
        int elevationNumber,
        DateTimeOffset messageTimestamp,
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
                messageTimestamp,
                sourceOrder);
        }
    }

    private void ProjectSixteenBitMomentBlock(
        ReadOnlySpan<byte> data,
        string momentName,
        Type31MomentMetadata metadata,
        int sweepSequenceNumber,
        int elevationNumber,
        DateTimeOffset messageTimestamp,
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
                messageTimestamp,
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
        DateTimeOffset messageTimestamp,
        ArchiveTwoRadialSourceOrder sourceOrder)
    {
        var status = ClassifyMomentValue(momentName, rawValue, metadata);
        var calibratedValue = status == ArchiveTwoGateMomentStatus.Valid
            ? (double?)((rawValue - metadata.Offset) / metadata.Scale)
            : null;
        acceptEvent(new ArchiveTwoGateMomentEvent(
            RadarId,
            VolumeTimestamp,
            messageTimestamp,
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
}
