using System.Buffers.Binary;
using RadarPulse.Domain.Archive;
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
    public void ExtractsType31MomentDescriptorMetadata()
    {
        var builder = new ArchiveTwoMessageSummaryBuilder();
        var scanner = new ArchiveTwoMessageStreamScanner(builder);

        scanner.Append(BuildMessage(31, BuildType31Payload(
            "REF",
            4,
            wordSizeBits: 8,
            momentDataByteCount: 0,
            firstGateRangeKilometers: 0.3f,
            gateSpacingKilometers: 0.25f,
            scale: 2f,
            offset: 66f)));
        scanner.Append(BuildMessage(31, BuildType31Payload(
            "REF",
            6,
            wordSizeBits: 16,
            momentDataByteCount: 0,
            firstGateRangeKilometers: 0.6f,
            gateSpacingKilometers: 1.0f,
            scale: 0.5f,
            offset: 100f)));
        scanner.Complete();

        var summary = builder.Build();
        var moment = Assert.Single(summary.Type31.Moments);
        Assert.Equal("REF", moment.Name);
        Assert.Equal(2, moment.RadialCount);
        Assert.Equal(10, moment.GateCount);
        Assert.Equal(4, moment.MinimumGateCount);
        Assert.Equal(6, moment.MaximumGateCount);
        Assert.Equal(8, moment.MinimumWordSizeBits);
        Assert.Equal(16, moment.MaximumWordSizeBits);
        Assert.Equal(0.3f, moment.MinimumFirstGateRangeKilometers, precision: 3);
        Assert.Equal(0.6f, moment.MaximumFirstGateRangeKilometers, precision: 3);
        Assert.Equal(0.25f, moment.MinimumGateSpacingKilometers, precision: 3);
        Assert.Equal(1.0f, moment.MaximumGateSpacingKilometers, precision: 3);
        Assert.Equal(0.5f, moment.MinimumScale, precision: 3);
        Assert.Equal(2f, moment.MaximumScale, precision: 3);
        Assert.Equal(66f, moment.MinimumOffset, precision: 3);
        Assert.Equal(100f, moment.MaximumOffset, precision: 3);
    }

    [Fact]
    public void SummarizesType31SweepsConstantBlocksAndSourceOrder()
    {
        var builder = new ArchiveTwoMessageSummaryBuilder();
        var scanner = new ArchiveTwoMessageStreamScanner(builder);

        scanner.Reset(sourceRecordSequenceNumber: 7);
        scanner.Append(BuildMessage(31, BuildType31PayloadWithConstantBlocks("REF", 4, radialStatus: 3, elevationNumber: 1, elevationAngleDegrees: 0.5f)));
        scanner.Append(BuildMessage(31, BuildType31PayloadWithConstantBlocks("REF", 4, radialStatus: 2, elevationNumber: 1, elevationAngleDegrees: 0.6f)));
        scanner.Append(BuildMessage(31, BuildType31PayloadWithConstantBlocks("VEL", 6, radialStatus: 5, elevationNumber: 2, elevationAngleDegrees: 1.5f)));
        scanner.Append(BuildMessage(31, BuildType31PayloadWithConstantBlocks("VEL", 6, radialStatus: 4, elevationNumber: 2, elevationAngleDegrees: 1.6f)));
        scanner.Complete();

        var summary = builder.Build();
        Assert.Equal(4, summary.Type31.RadialCount);
        Assert.Equal(20, summary.Type31.EstimatedGateMomentEventCount);
        Assert.Equal(new ArchiveTwoType31ConstantBlockSummary(4, 4, 4), summary.Type31.ConstantBlocks);

        Assert.Collection(
            summary.Type31.Sweeps,
            first =>
            {
                Assert.Equal(1, first.SequenceNumber);
                Assert.Equal(1, first.ElevationNumber);
                Assert.Equal(0, first.MinimumCutSectorNumber);
                Assert.Equal(0, first.MaximumCutSectorNumber);
                Assert.Equal(2, first.RadialCount);
                Assert.Equal(3, first.StartRadialStatus);
                Assert.Equal(2, first.EndRadialStatus);
                Assert.Equal(0.5f, first.MinimumElevationAngleDegrees, precision: 3);
                Assert.Equal(0.6f, first.MaximumElevationAngleDegrees, precision: 3);
                Assert.Equal(0.55f, first.AverageElevationAngleDegrees, precision: 3);
                Assert.Equal(2, first.VolumeConstantBlockCount);
                Assert.Equal(2, first.ElevationConstantBlockCount);
                Assert.Equal(2, first.RadialConstantBlockCount);
                Assert.Equal(["REF"], first.Moments);
                Assert.Equal(new ArchiveTwoRadialSourceOrder(7, 1, 1), first.FirstRadial);
                Assert.Equal(new ArchiveTwoRadialSourceOrder(7, 2, 2), first.LastRadial);
            },
            second =>
            {
                Assert.Equal(2, second.SequenceNumber);
                Assert.Equal(2, second.ElevationNumber);
                Assert.Equal(0, second.MinimumCutSectorNumber);
                Assert.Equal(0, second.MaximumCutSectorNumber);
                Assert.Equal(2, second.RadialCount);
                Assert.Equal(5, second.StartRadialStatus);
                Assert.Equal(4, second.EndRadialStatus);
                Assert.Equal(1.5f, second.MinimumElevationAngleDegrees, precision: 3);
                Assert.Equal(1.6f, second.MaximumElevationAngleDegrees, precision: 3);
                Assert.Equal(1.55f, second.AverageElevationAngleDegrees, precision: 3);
                Assert.Equal(["VEL"], second.Moments);
                Assert.Equal(new ArchiveTwoRadialSourceOrder(7, 3, 3), second.FirstRadial);
                Assert.Equal(new ArchiveTwoRadialSourceOrder(7, 4, 4), second.LastRadial);
            });
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
