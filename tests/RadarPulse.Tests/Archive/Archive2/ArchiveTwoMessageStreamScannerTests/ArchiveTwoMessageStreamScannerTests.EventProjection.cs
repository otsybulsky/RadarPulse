using System.Buffers.Binary;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class ArchiveTwoMessageStreamScannerTests
{
    [Fact]
    public void ProjectsGateMomentEventsWithRangeStatusCalibrationAndSourceOrder()
    {
        var events = new List<ArchiveTwoGateMomentEvent>();
        var projector = new ArchiveTwoGateMomentEventProjector(
            "KTLX",
            new DateTimeOffset(2026, 5, 4, 0, 2, 45, TimeSpan.Zero),
            events.Add);
        var scanner = new ArchiveTwoMessageStreamScanner(projector);

        scanner.Reset(sourceRecordSequenceNumber: 7);
        scanner.Append(BuildMessage(31, BuildEightBitType31Payload(
            "REF",
            [0, 1, 2, 66, 68],
            scale: 2f,
            offset: 66f)));
        scanner.Complete();

        Assert.Collection(
            events,
            below =>
            {
                Assert.Equal("KTLX", below.RadarId);
                Assert.Equal(new DateTimeOffset(2026, 5, 4, 0, 2, 44, 18, TimeSpan.Zero), below.MessageTimestamp);
                Assert.Equal(ArchiveTwoGateMomentStatus.BelowThreshold, below.Status);
                Assert.Null(below.CalibratedValue);
                Assert.Equal(0, below.GateIndex);
                Assert.Equal(0.3f, below.RangeKilometers, precision: 3);
                Assert.Equal(new ArchiveTwoRadialSourceOrder(7, 1, 1), below.SourceOrder);
            },
            folded =>
            {
                Assert.Equal(ArchiveTwoGateMomentStatus.RangeFolded, folded.Status);
                Assert.Null(folded.CalibratedValue);
                Assert.Equal(1, folded.GateIndex);
                Assert.Equal(0.55f, folded.RangeKilometers, precision: 3);
            },
            validNegative =>
            {
                Assert.Equal(ArchiveTwoGateMomentStatus.Valid, validNegative.Status);
                Assert.Equal(-32, validNegative.CalibratedValue);
                Assert.Equal(2, validNegative.GateIndex);
                Assert.Equal(0.8f, validNegative.RangeKilometers, precision: 3);
            },
            validZero =>
            {
                Assert.Equal(ArchiveTwoGateMomentStatus.Valid, validZero.Status);
                Assert.Equal(0, validZero.CalibratedValue);
                Assert.Equal(3, validZero.GateIndex);
                Assert.Equal(1.05f, validZero.RangeKilometers, precision: 3);
            },
            validPositive =>
            {
                Assert.Equal(ArchiveTwoGateMomentStatus.Valid, validPositive.Status);
                Assert.Equal(1, validPositive.CalibratedValue);
                Assert.Equal(4, validPositive.GateIndex);
                Assert.Equal(1.3f, validPositive.RangeKilometers, precision: 3);
            });
    }
}
