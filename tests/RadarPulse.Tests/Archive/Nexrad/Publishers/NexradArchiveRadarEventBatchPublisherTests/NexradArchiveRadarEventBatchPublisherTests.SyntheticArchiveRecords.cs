using System.Buffers.Binary;
using System.Text;

namespace RadarPulse.Tests.Archive;

public sealed partial class NexradArchiveRadarEventBatchPublisherTests
{
    private static byte[] BuildArchiveTwoHeader(
        string radarId = "KTLX",
        DateOnly? date = null,
        int millisecondsPastMidnight = 164_018)
    {
        var effectiveDate = date ?? new DateOnly(2026, 5, 4);
        var header = new byte[24];
        Encoding.ASCII.GetBytes("AR2V0006.266").CopyTo(header, 0);
        BinaryPrimitives.WriteInt32BigEndian(
            header.AsSpan(12, 4),
            effectiveDate.DayNumber - new DateOnly(1970, 1, 1).DayNumber + 1);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(16, 4), millisecondsPastMidnight);
        Encoding.ASCII.GetBytes(radarId).CopyTo(header, 20);
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

    private static byte[] BuildMessage(
        byte messageType,
        byte[] payload,
        DateOnly? date = null,
        uint? millisecondsPastMidnight = null)
    {
        var effectiveDate = date ?? new DateOnly(2026, 5, 4);
        var effectiveMillisecondsPastMidnight = millisecondsPastMidnight ?? 164_018;
        var messageBytes = 16 + payload.Length;
        if (messageBytes % 2 != 0)
        {
            throw new ArgumentException("Synthetic message length must be even.", nameof(payload));
        }

        var message = new byte[messageBytes];
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(0, 2), (ushort)(messageBytes / 2));
        message[2] = 8;
        message[3] = messageType;
        BinaryPrimitives.WriteUInt16BigEndian(
            message.AsSpan(6, 2),
            checked((ushort)(effectiveDate.DayNumber - new DateOnly(1970, 1, 1).DayNumber + 1)));
        BinaryPrimitives.WriteUInt32BigEndian(message.AsSpan(8, 4), effectiveMillisecondsPastMidnight);
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(12, 2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(14, 2), 1);
        payload.CopyTo(message.AsSpan(16));
        return message;
    }

    private static byte[] BuildEightBitType31Payload(
        string momentName,
        byte[] values,
        float scale,
        float offset)
    {
        var payload = BuildType31Payload(
            momentName,
            checked((ushort)values.Length),
            wordSizeBits: 8,
            values.Length,
            scale,
            offset);
        values.CopyTo(payload.AsSpan(100));
        return payload;
    }

    private static byte[] BuildSixteenBitType31Payload(
        string momentName,
        ushort[] values,
        float scale,
        float offset)
    {
        var payload = BuildType31Payload(
            momentName,
            checked((ushort)values.Length),
            wordSizeBits: 16,
            values.Length * sizeof(ushort),
            scale,
            offset);
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
        float scale,
        float offset)
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
        payload[momentOffset + 19] = wordSizeBits;
        WriteSingleBigEndian(payload.AsSpan(momentOffset + 20, 4), scale);
        WriteSingleBigEndian(payload.AsSpan(momentOffset + 24, 4), offset);
        return payload;
    }

    private static void WriteSingleBigEndian(Span<byte> destination, float value) =>
        BinaryPrimitives.WriteInt32BigEndian(destination, BitConverter.SingleToInt32Bits(value));
}
