using System.Buffers.Binary;
using System.Text;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Archive;

internal sealed partial class ArchiveTwoRadarEventBatchProjector : IArchiveTwoMessageConsumer
{
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
        radialSequenceNumber++;

        var elevationSlot = Math.Max(parsePayload[22] - 1, 0);
        var azimuthBucket = GetAzimuthBucket(radialSequenceNumber);
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

            AcceptMomentBlock(block, source, elevationSlot, azimuthBucket);
        }
    }

    private int GetAzimuthBucket(int radialSequence)
    {
        // First integration uses deterministic radial order buckets. A later
        // parser slice can swap in decoded azimuth angle without changing the
        // stream contract or SourceId arithmetic.
        return (radialSequence - 1) % azimuthBucketCount;
    }
}
