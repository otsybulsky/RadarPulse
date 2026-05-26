using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingHandlerOutputContractTests
{
    [Fact]
    public void HandlerFreeContractAllowsOrderedConcurrentDelta()
    {
        var contract = RadarProcessingHandlerOutputContract.FromHandlers(
            Array.Empty<IRadarSourceProcessingHandler>());

        Assert.Equal(
            RadarProcessingHandlerStatePosture.HandlerFreeOrderedConcurrent,
            contract.StatePosture);
        Assert.False(contract.HasHandlers);
        Assert.True(contract.AllowsOrderedConcurrentDelta);
        Assert.False(contract.RequiresSequentialFallback);
        Assert.Empty(contract.Handlers);
        Assert.Contains("ordered concurrent", contract.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StatefulHandlersUseSnapshotExportWithSequentialFallback()
    {
        var contract = RadarProcessingHandlerOutputContract.FromHandlers(
            new IRadarSourceProcessingHandler[] { new CountingHandler() });

        Assert.Equal(
            RadarProcessingHandlerStatePosture.StatefulSnapshotSequentialFallback,
            contract.StatePosture);
        Assert.True(contract.HasHandlers);
        Assert.False(contract.AllowsOrderedConcurrentDelta);
        Assert.True(contract.RequiresSequentialFallback);
        Assert.Contains("sequential fallback", contract.Message, StringComparison.Ordinal);

        var handler = Assert.Single(contract.Handlers);
        Assert.Equal(0, handler.HandlerIndex);
        Assert.Equal("counting", handler.Name);
        Assert.Equal(3, handler.Int64SlotCount);
        Assert.Equal(1, handler.DoubleSlotCount);
        Assert.Equal(4, handler.Fields.Count);
        Assert.Equal("events", handler.Fields[0].Name);
        Assert.Equal(RadarSourceProcessingSnapshotFieldType.Int64, handler.Fields[0].Type);
        Assert.Equal("last.scale", handler.Fields[3].Name);
        Assert.Equal(RadarSourceProcessingSnapshotFieldType.Double, handler.Fields[3].Type);
    }

    [Fact]
    public void ContractRejectsDuplicateHandlerNames()
    {
        var handlers = new IRadarSourceProcessingHandler[]
        {
            new CountingHandler("same", eventFieldName: "first.events"),
            new CountingHandler("same", eventFieldName: "second.events")
        };

        Assert.Throws<ArgumentException>(() =>
            RadarProcessingHandlerOutputContract.FromHandlers(handlers));
    }

    [Fact]
    public void ContractRejectsDuplicateOutputFieldNamesAcrossHandlers()
    {
        var handlers = new IRadarSourceProcessingHandler[]
        {
            new CountingHandler("first", eventFieldName: "events"),
            new CountingHandler("second", eventFieldName: "events")
        };

        Assert.Throws<ArgumentException>(() =>
            RadarProcessingHandlerOutputContract.FromHandlers(handlers));
    }

    [Fact]
    public void ContractCanBeCreatedFromCoreOptions()
    {
        var options = new RadarProcessingCoreOptions(
            handlers: new IRadarSourceProcessingHandler[] { new CountingHandler() });

        var contract = RadarProcessingHandlerOutputContract.FromOptions(options);

        Assert.True(contract.HasHandlers);
        Assert.True(contract.RequiresSequentialFallback);
    }

    private sealed class CountingHandler : IRadarSourceProcessingHandler
    {
        public CountingHandler(
            string name = "counting",
            string eventFieldName = "events",
            string payloadFieldName = "payload.values",
            string checksumFieldName = "raw.checksum",
            string scaleFieldName = "last.scale")
        {
            Descriptor = new RadarSourceProcessingHandlerDescriptor(
                name,
                int64SlotCount: 3,
                doubleSlotCount: 1,
                new[]
                {
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        eventFieldName,
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 0),
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        payloadFieldName,
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 1),
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        checksumFieldName,
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 2),
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        scaleFieldName,
                        RadarSourceProcessingSnapshotFieldType.Double,
                        slotIndex: 0)
                });
        }

        public RadarSourceProcessingHandlerDescriptor Descriptor { get; }

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            state.AddInt64(slotIndex: 0, value: 1);
            state.AddInt64(slotIndex: 1, context.PayloadMetrics.PayloadValueCount);
            state.AddInt64(slotIndex: 2, context.PayloadMetrics.RawValueChecksum);
            state.SetDouble(slotIndex: 0, context.StreamEvent.Scale);
        }
    }
}

