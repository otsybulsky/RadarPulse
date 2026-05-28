using System.Buffers.Binary;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class ArchiveTwoGateMomentEventProjector
{
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

            ProjectMomentBlock(block, sweepSequenceNumber, radial.ElevationNumber, source.MessageTimestamp, sourceOrder);
        }
    }
}
