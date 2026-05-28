using System.Buffers.Binary;
using System.Text;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class ArchiveTwoMessageSummaryBuilder
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
        var sourceOrder = new ArchiveTwoRadialSourceOrder(
            source.CompressedRecordSequenceNumber,
            source.MessageSequenceNumberInRecord,
            type31RadialCount + 1);
        var sweep = AcceptRadial(radial, sourceOrder);
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
            if (block.Length < 4)
            {
                continue;
            }

            var blockName = ReadDataBlockName(block);
            if (block[0] == (byte)'R')
            {
                AcceptConstantBlock(blockName, sweep);
                continue;
            }

            if (block[0] == (byte)'D')
            {
                AcceptMomentBlock(block, blockName, sweep);
            }
        }

        type31RadialCount++;
    }

    private static Type31RadialMetadata ReadType31RadialMetadata(ReadOnlySpan<byte> payload) =>
        new(
            payload[21],
            payload[22],
            payload[23],
            ReadSingleBigEndian(payload.Slice(24, 4)));

    private SweepAccumulator? AcceptRadial(
        Type31RadialMetadata radial,
        ArchiveTwoRadialSourceOrder sourceOrder)
    {
        if (!collectSweepSummaries)
        {
            return null;
        }

        if (currentSweep is null ||
            ShouldStartNewSweep(radial, currentSweep))
        {
            currentSweep = new SweepAccumulator(
                sweeps.Count + 1,
                radial.ElevationNumber,
                radial.RadialStatus,
                sourceOrder);
            sweeps.Add(currentSweep);
        }

        currentSweep.AcceptRadial(radial, sourceOrder);
        return currentSweep;
    }

    private static bool ShouldStartNewSweep(Type31RadialMetadata radial, SweepAccumulator current) =>
        current.RadialCount > 0 &&
        (IsStartRadialStatus(radial.RadialStatus) ||
            radial.ElevationNumber != current.ElevationNumber);

    private static bool IsStartRadialStatus(int radialStatus) =>
        radialStatus is 0 or 3 or 5 or 80 or 83 or 85;

    private void AcceptConstantBlock(string blockName, SweepAccumulator? sweep)
    {
        switch (blockName)
        {
            case "VOL":
                volumeConstantBlockCount++;
                sweep?.AcceptVolumeConstantBlock();
                break;
            case "ELV":
                elevationConstantBlockCount++;
                sweep?.AcceptElevationConstantBlock();
                break;
            case "RAD":
                radialConstantBlockCount++;
                sweep?.AcceptRadialConstantBlock();
                break;
        }
    }

    private void AcceptMomentBlock(
        ReadOnlySpan<byte> block,
        string name,
        SweepAccumulator? sweep)
    {
        if (name.Length == 0 ||
            block.Length < GenericMomentDescriptorLength)
        {
            return;
        }

        var metadata = ReadMomentMetadata(block);
        moments.TryGetValue(name, out var accumulator);
        accumulator ??= new MomentAccumulator();
        accumulator.Add(metadata);
        moments[name] = accumulator;
        sweep?.AcceptMoment(name);
        estimatedGateMomentEvents += metadata.GateCount;
        if (decodeMomentValues)
        {
            DecodeMomentValues(name, block, metadata);
        }
    }

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

}
