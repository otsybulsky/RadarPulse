using System.Buffers.Binary;
using System.Text;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Archive;

internal sealed partial class ArchiveTwoRadarEventBatchProjector : IArchiveTwoMessageConsumer
{
    private static int GetMomentCode(ReadOnlySpan<byte> momentNameUtf8)
    {
        var code = momentNameUtf8.Length << 24;
        for (var i = 0; i < momentNameUtf8.Length; i++)
        {
            code |= momentNameUtf8[i] << (i * 8);
        }

        return code;
    }

    private static ReadOnlySpan<byte> ReadDataBlockNameUtf8(ReadOnlySpan<byte> block)
    {
        var name = block.Slice(1, 3);
        var length = name.Length;
        while (length > 0 && name[length - 1] is 0 or (byte)' ')
        {
            length--;
        }

        return name[..length];
    }

    private static Type31MomentMetadata ReadMomentMetadata(ReadOnlySpan<byte> block) =>
        new(
            BinaryPrimitives.ReadUInt16BigEndian(block.Slice(GenericMomentGateCountOffset, 2)),
            block[GenericMomentWordSizeOffset],
            ReadSingleBigEndian(block.Slice(GenericMomentScaleOffset, 4)),
            ReadSingleBigEndian(block.Slice(GenericMomentOffsetOffset, 4)));

    private static float ReadSingleBigEndian(ReadOnlySpan<byte> buffer) =>
        BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(buffer));

    private readonly record struct Type31MomentMetadata(
        int GateCount,
        int WordSizeBits,
        float Scale,
        float Offset);

    private readonly record struct CachedIdentityDimensions(
        ushort RadarOrdinal,
        ushort MomentId,
        int RadarSourceBlockStart);
}
