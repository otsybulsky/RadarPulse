using System.Buffers.Binary;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class ArchiveTwoMessageStreamScannerTests
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
}
