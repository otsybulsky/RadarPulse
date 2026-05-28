using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarSourceProcessingHandlerSlotTests
{
    [Fact]
    public void LayoutAssignsNonOverlappingSlotOffsets()
    {
        var first = new CountingHandler(
            "first",
            eventFieldName: "first.events",
            payloadFieldName: "first.payload",
            checksumFieldName: "first.checksum",
            scaleFieldName: "first.scale");
        var second = new CountingHandler(
            "second",
            eventFieldName: "second.events",
            payloadFieldName: "second.payload",
            checksumFieldName: "second.checksum",
            scaleFieldName: "second.scale");

        var layout = new RadarSourceProcessingHandlerSlotLayout(new IRadarSourceProcessingHandler[] { first, second });

        Assert.Equal(6, layout.TotalInt64SlotCount);
        Assert.Equal(2, layout.TotalDoubleSlotCount);
        Assert.Equal(8, layout.SnapshotFieldCount);
        Assert.Equal(0, layout.Assignments[0].Int64SlotOffset);
        Assert.Equal(0, layout.Assignments[0].DoubleSlotOffset);
        Assert.Equal(3, layout.Assignments[1].Int64SlotOffset);
        Assert.Equal(1, layout.Assignments[1].DoubleSlotOffset);
    }

    [Fact]
    public void LayoutRejectsDuplicateSnapshotFieldNames()
    {
        var first = new CountingHandler(
            "first",
            eventFieldName: "events",
            payloadFieldName: "first.payload",
            checksumFieldName: "first.checksum",
            scaleFieldName: "first.scale");
        var second = new CountingHandler(
            "second",
            eventFieldName: "events",
            payloadFieldName: "second.payload",
            checksumFieldName: "second.checksum",
            scaleFieldName: "second.scale");

        Assert.Throws<ArgumentException>(() =>
            new RadarSourceProcessingHandlerSlotLayout(new IRadarSourceProcessingHandler[] { first, second }));
    }
}
