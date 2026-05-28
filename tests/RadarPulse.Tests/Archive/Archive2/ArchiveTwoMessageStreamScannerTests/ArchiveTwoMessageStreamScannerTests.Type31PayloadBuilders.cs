using System.Buffers.Binary;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class ArchiveTwoMessageStreamScannerTests
{
    private static byte[] BuildType31Payload(string momentName, ushort gates)
    {
        var payload = BuildType31Payload(momentName, gates, wordSizeBits: 8, momentDataByteCount: 0);
        return payload;
    }

    private static byte[] BuildType31PayloadWithConstantBlocks(
        string momentName,
        ushort gates,
        byte radialStatus,
        byte elevationNumber,
        float elevationAngleDegrees)
    {
        var payload = BuildType31Payload(
            momentName,
            gates,
            wordSizeBits: 8,
            momentDataByteCount: 0,
            includeConstantBlocks: true,
            radialStatus,
            elevationNumber,
            elevationAngleDegrees);
        return payload;
    }

    private static byte[] BuildEightBitType31Payload(
        string momentName,
        byte[] values,
        float scale = 2f,
        float offset = 66f)
    {
        var payload = BuildType31Payload(
            momentName,
            checked((ushort)values.Length),
            wordSizeBits: 8,
            values.Length,
            scale: scale,
            offset: offset);
        values.CopyTo(payload.AsSpan(100));
        return payload;
    }

    private static byte[] BuildSixteenBitType31Payload(string momentName, ushort[] values)
    {
        var payload = BuildType31Payload(momentName, checked((ushort)values.Length), wordSizeBits: 16, values.Length * sizeof(ushort));
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(100 + i * sizeof(ushort), sizeof(ushort)), values[i]);
        }

        return payload;
    }

    private static byte[] BuildType31Payload(
        string momentName,
        ushort gates,
        byte wordSizeBits,
        int momentDataByteCount,
        bool includeConstantBlocks = false,
        byte radialStatus = 0,
        byte elevationNumber = 1,
        float elevationAngleDegrees = 0,
        float firstGateRangeKilometers = 0.3f,
        float gateSpacingKilometers = 0.25f,
        float scale = 2f,
        float offset = 66f)
    {
        var momentOffset = includeConstantBlocks ? 136 : 72;
        var payload = new byte[Math.Max(momentOffset + 28 + momentDataByteCount, 160)];
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(18, 2), (ushort)payload.Length);
        payload[21] = radialStatus;
        payload[22] = elevationNumber;
        WriteSingleBigEndian(payload.AsSpan(24, 4), elevationAngleDegrees);

        if (includeConstantBlocks)
        {
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(30, 2), 4);
            BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(32, 4), 72);
            BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(36, 4), 88);
            BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(40, 4), 104);
            BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(44, 4), momentOffset);
            WriteConstantBlock(payload.AsSpan(72), "VOL", 16);
            WriteConstantBlock(payload.AsSpan(88), "ELV", 16);
            WriteConstantBlock(payload.AsSpan(104), "RAD", 28);
        }
        else
        {
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(30, 2), 1);
            BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(32, 4), momentOffset);
        }

        payload[momentOffset] = (byte)'D';
        for (var i = 0; i < momentName.Length && i < 3; i++)
        {
            payload[momentOffset + 1 + i] = (byte)momentName[i];
        }

        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(momentOffset + 8, 2), gates);
        WriteScaledKilometers(payload.AsSpan(momentOffset + 10, 2), firstGateRangeKilometers);
        WriteScaledKilometers(payload.AsSpan(momentOffset + 12, 2), gateSpacingKilometers);
        payload[momentOffset + 19] = wordSizeBits;
        WriteSingleBigEndian(payload.AsSpan(momentOffset + 20, 4), scale);
        WriteSingleBigEndian(payload.AsSpan(momentOffset + 24, 4), offset);
        return payload;
    }

    private static void WriteConstantBlock(Span<byte> destination, string name, ushort length)
    {
        destination[0] = (byte)'R';
        for (var i = 0; i < name.Length && i < 3; i++)
        {
            destination[1 + i] = (byte)name[i];
        }

        BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(4, 2), length);
    }

    private static void WriteSingleBigEndian(Span<byte> destination, float value) =>
        BinaryPrimitives.WriteInt32BigEndian(destination, BitConverter.SingleToInt32Bits(value));

    private static void WriteScaledKilometers(Span<byte> destination, float kilometers) =>
        BinaryPrimitives.WriteUInt16BigEndian(destination, checked((ushort)MathF.Round(kilometers * 1_000f)));
}
