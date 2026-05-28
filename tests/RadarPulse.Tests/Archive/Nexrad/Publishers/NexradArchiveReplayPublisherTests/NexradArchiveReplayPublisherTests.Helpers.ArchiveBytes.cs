using System.Buffers.Binary;
using System.Text;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class NexradArchiveReplayPublisherTests
{
    private static byte[] BuildArchiveTwoHeader()
    {
        var header = new byte[24];
        Encoding.ASCII.GetBytes("AR2V0006.266").CopyTo(header, 0);
        BinaryPrimitives.WriteInt32BigEndian(
            header.AsSpan(12, 4),
            new DateOnly(2026, 5, 4).DayNumber - new DateOnly(1970, 1, 1).DayNumber + 1);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(16, 4), 164_018);
        Encoding.ASCII.GetBytes("KTLX").CopyTo(header, 20);
        return header;
    }

    private static byte[] BuildCompressedRecord(int controlWord, byte[] compressedPayload) =>
        BuildCompressedRecordControlWord(controlWord).Concat(compressedPayload).ToArray();

    private static byte[] BuildCompressedRecordControlWord(int controlWord)
    {
        var buffer = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, controlWord);
        return buffer;
    }

    private static byte[] BuildFakeBZip2Payload(byte key) => [(byte)'B', (byte)'Z', (byte)'h', key];

    private static byte[] BuildMessage(byte messageType, byte[] payload)
    {
        var messageBytes = 16 + payload.Length;
        if (messageBytes % 2 != 0)
        {
            throw new ArgumentException("Synthetic message length must be even.", nameof(payload));
        }

        var message = new byte[messageBytes];
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(0, 2), (ushort)(messageBytes / 2));
        message[2] = 8;
        message[3] = messageType;
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(6, 2), 20_578);
        BinaryPrimitives.WriteUInt32BigEndian(message.AsSpan(8, 4), 164_018);
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(12, 2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(14, 2), 1);
        payload.CopyTo(message.AsSpan(16));
        return message;
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

    private static byte[] BuildType31Payload(
        string momentName,
        ushort gates,
        byte wordSizeBits,
        int momentDataByteCount,
        float firstGateRangeKilometers = 0.3f,
        float gateSpacingKilometers = 0.25f,
        float scale = 2f,
        float offset = 66f)
    {
        const int momentOffset = 72;
        var payload = new byte[Math.Max(momentOffset + 28 + momentDataByteCount, 160)];
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(18, 2), (ushort)payload.Length);
        payload[22] = 1;
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(30, 2), 1);
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(32, 4), momentOffset);

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

    private static void WriteSingleBigEndian(Span<byte> destination, float value) =>
        BinaryPrimitives.WriteInt32BigEndian(destination, BitConverter.SingleToInt32Bits(value));

    private static void WriteScaledKilometers(Span<byte> destination, float kilometers) =>
        BinaryPrimitives.WriteUInt16BigEndian(destination, checked((ushort)MathF.Round(kilometers * 1_000f)));

}
