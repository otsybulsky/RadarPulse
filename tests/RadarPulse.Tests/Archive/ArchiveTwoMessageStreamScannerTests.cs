using System.Buffers.Binary;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed class ArchiveTwoMessageStreamScannerTests
{
    [Fact]
    public void CountsMessageHeadersAcrossChunkBoundaries()
    {
        var builder = new ArchiveTwoMessageSummaryBuilder();
        var scanner = new ArchiveTwoMessageStreamScanner(builder);
        var bytes = new byte[] { 0, 0, 0, 0, 0, 0 }
            .Concat(BuildMessage(15, new byte[10]))
            .Concat(new byte[] { 0, 0, 0, 0 })
            .Concat(BuildMessage(31, BuildType31Payload("REF", 100)))
            .Concat(new byte[] { 0, 0, 0 })
            .ToArray();

        scanner.Append(bytes.AsSpan(0, 5));
        scanner.Append(bytes.AsSpan(5, 17));
        scanner.Append(bytes.AsSpan(22));
        scanner.Complete();

        var summary = builder.Build();
        Assert.Equal(2, summary.MessageCount);
        Assert.Collection(
            summary.MessageTypes,
            type15 =>
            {
                Assert.Equal(15, type15.MessageType);
                Assert.Equal(1, type15.Count);
            },
            type31 =>
            {
                Assert.Equal(31, type31.MessageType);
                Assert.Equal(1, type31.Count);
            });
    }

    [Fact]
    public void ExtractsType31MomentGateCounts()
    {
        var builder = new ArchiveTwoMessageSummaryBuilder();
        var scanner = new ArchiveTwoMessageStreamScanner(builder);

        scanner.Append(BuildMessage(31, BuildType31Payload("VEL", 920)));
        scanner.Complete();

        var summary = builder.Build();
        Assert.Equal(1, summary.Type31.RadialCount);
        Assert.Equal(920, summary.Type31.EstimatedGateMomentEventCount);
        var moment = Assert.Single(summary.Type31.Moments);
        Assert.Equal("VEL", moment.Name);
        Assert.Equal(1, moment.RadialCount);
        Assert.Equal(920, moment.GateCount);
    }

    [Fact]
    public void DecodesEightBitType31MomentValues()
    {
        var builder = new ArchiveTwoMessageSummaryBuilder(decodeMomentValues: true);
        var scanner = new ArchiveTwoMessageStreamScanner(builder);

        scanner.Append(BuildMessage(31, BuildEightBitType31Payload("REF", [0, 1, 2, 255])));
        scanner.Complete();

        Assert.Equal(4, builder.EstimatedGateMomentEventCount);
        Assert.Equal(4, builder.DecodedGateMomentValueCount);
        Assert.Equal((ulong)(0 + 1 + 2 + 255), builder.DecodedGateMomentValueChecksum);
    }

    [Fact]
    public void DecodesSixteenBitType31MomentValues()
    {
        var builder = new ArchiveTwoMessageSummaryBuilder(decodeMomentValues: true);
        var scanner = new ArchiveTwoMessageStreamScanner(builder);

        scanner.Append(BuildMessage(31, BuildSixteenBitType31Payload("PHI", [0, 1, 500, 65535])));
        scanner.Complete();

        Assert.Equal(4, builder.EstimatedGateMomentEventCount);
        Assert.Equal(4, builder.DecodedGateMomentValueCount);
        Assert.Equal((ulong)(0 + 1 + 500 + 65_535), builder.DecodedGateMomentValueChecksum);
    }

    [Fact]
    public void IgnoresShortNonMessageTail()
    {
        var builder = new ArchiveTwoMessageSummaryBuilder();
        var scanner = new ArchiveTwoMessageStreamScanner(builder);

        scanner.Append([0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17]);
        scanner.Complete();

        Assert.Equal(0, builder.Build().MessageCount);
    }

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

    private static byte[] BuildType31Payload(string momentName, ushort gates)
    {
        var payload = BuildType31Payload(momentName, gates, wordSizeBits: 8, momentDataByteCount: 0);
        return payload;
    }

    private static byte[] BuildEightBitType31Payload(string momentName, byte[] values)
    {
        var payload = BuildType31Payload(momentName, checked((ushort)values.Length), wordSizeBits: 8, values.Length);
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
        int momentDataByteCount)
    {
        var payload = new byte[Math.Max(128, 100 + momentDataByteCount)];
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(18, 2), (ushort)payload.Length);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(30, 2), 1);
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(32, 4), 72);

        payload[72] = (byte)'D';
        for (var i = 0; i < momentName.Length && i < 3; i++)
        {
            payload[73 + i] = (byte)momentName[i];
        }

        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(80, 2), gates);
        payload[91] = wordSizeBits;
        return payload;
    }
}
