using System.Buffers.Binary;
using System.Text;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Archive;

internal sealed partial class ArchiveTwoRadarEventBatchProjector : IArchiveTwoMessageConsumer
{
    private void AcceptMomentBlock(
        ReadOnlySpan<byte> block,
        ArchiveTwoMessageSource source,
        int elevationSlot,
        int azimuthBucket)
    {
        var momentName = ReadDataBlockNameUtf8(block);
        if (momentName.Length == 0)
        {
            return;
        }

        var metadata = ReadMomentMetadata(block);
        var wordSize = metadata.WordSizeBits switch
        {
            8 => RadarStreamWordSize.EightBit,
            16 => RadarStreamWordSize.SixteenBit,
            _ => default
        };
        if (wordSize == 0)
        {
            return;
        }

        var bytesPerGate = wordSize == RadarStreamWordSize.EightBit ? 1 : sizeof(ushort);
        var requiredBytes = checked(metadata.GateCount * bytesPerGate);
        var data = block[GenericMomentDataOffset..];
        if (data.Length < requiredBytes)
        {
            return;
        }

        for (var rangeBand = 0; rangeBand < rangeBandCount; rangeBand++)
        {
            var startGate = rangeBand * metadata.GateCount / rangeBandCount;
            var endGate = (rangeBand + 1) * metadata.GateCount / rangeBandCount;
            if (endGate <= startGate)
            {
                continue;
            }

            if (startGate > ushort.MaxValue || endGate - startGate > ushort.MaxValue)
            {
                throw new InvalidDataException("Type 31 moment gate run exceeds the stream event range.");
            }

            var identity = ResolveIdentity(momentName, elevationSlot, azimuthBucket, rangeBand);
            var payloadOffset = startGate * bytesPerGate;
            var payloadLength = (endGate - startGate) * bytesPerGate;
            batchBuilder.AddEvent(
                identity,
                volumeTimestampUtcTicks,
                source.MessageTimestamp.UtcTicks,
                source.CompressedRecordSequenceNumber,
                source.MessageSequenceNumberInRecord,
                radialSequenceNumber,
                checked((ushort)startGate),
                checked((ushort)(endGate - startGate)),
                wordSize,
                metadata.Scale,
                metadata.Offset,
                RadarStreamStatusModel.ArchiveTwoMoment,
                data.Slice(payloadOffset, payloadLength));
        }
    }
}
