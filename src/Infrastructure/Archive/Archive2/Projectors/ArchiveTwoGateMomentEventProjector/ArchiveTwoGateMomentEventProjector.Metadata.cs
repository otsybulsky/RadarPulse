using System.Buffers.Binary;
using System.Text;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class ArchiveTwoGateMomentEventProjector
{
    private static Type31RadialMetadata ReadType31RadialMetadata(ReadOnlySpan<byte> payload) =>
        new(
            payload[21],
            payload[22]);

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
