using System.Buffers.Binary;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class ArchiveTwoMessageStreamScannerTests
{
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
    public void DecodesCalibratedMomentValuesAndStatusCodes()
    {
        var builder = new ArchiveTwoMessageSummaryBuilder(decodeCalibratedMomentValues: true);
        var scanner = new ArchiveTwoMessageStreamScanner(builder);

        scanner.Append(BuildMessage(31, BuildEightBitType31Payload("REF", [0, 1, 2, 66, 68], scale: 2f, offset: 66f)));
        scanner.Append(BuildMessage(31, BuildEightBitType31Payload("CFP", [0, 1, 2, 3, 7, 8, 10], scale: 1f, offset: 8f)));
        scanner.Complete();

        Assert.Equal(12, builder.DecodedGateMomentValueCount);
        Assert.Equal((ulong)(0 + 1 + 2 + 66 + 68 + 0 + 1 + 2 + 3 + 7 + 8 + 10), builder.DecodedGateMomentValueChecksum);
        Assert.Equal(5, builder.CalibratedGateMomentValueCount);
        Assert.Equal(1, builder.BelowThresholdGateMomentValueCount);
        Assert.Equal(1, builder.RangeFoldedGateMomentValueCount);
        Assert.Equal(1, builder.ClutterFilterNotAppliedGateMomentValueCount);
        Assert.Equal(1, builder.PointClutterFilterAppliedGateMomentValueCount);
        Assert.Equal(1, builder.DualPolarizationFilteredGateMomentValueCount);
        Assert.Equal(2, builder.ReservedGateMomentValueCount);
        Assert.Equal(0, builder.UnsupportedCalibratedGateMomentValueCount);
        Assert.Equal(-29_000, builder.CalibratedGateMomentValueScaledChecksum);
        Assert.Equal(-32, builder.MinimumCalibratedGateMomentValue);
        Assert.Equal(2, builder.MaximumCalibratedGateMomentValue);
    }
}
