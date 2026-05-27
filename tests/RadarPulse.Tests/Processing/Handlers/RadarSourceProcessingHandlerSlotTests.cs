using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarSourceProcessingHandlerSlotTests
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

    [Fact]
    public void StateStoreKeepsHandlerStateIsolatedBySource()
    {
        var handler = new CountingHandler();
        var store = new RadarSourceProcessingStateStore(
            CreateUniverse(sourceCount: 2),
            new IRadarSourceProcessingHandler[] { handler });
        var firstBatch = CreateBatch(new[] { 0, 1, 0 });

        ApplyEvent(store, firstBatch, eventIndex: 0);
        ApplyEvent(store, firstBatch, eventIndex: 1);
        ApplyEvent(store, firstBatch, eventIndex: 2);

        var sourceZero = store.GetHandlerSnapshot(sourceId: 0);
        var sourceOne = store.GetHandlerSnapshot(sourceId: 1);

        Assert.Equal(2, GetInt64(sourceZero, "events"));
        Assert.Equal(2, GetInt64(sourceZero, "payload.values"));
        Assert.Equal(4, GetInt64(sourceZero, "raw.checksum"));
        Assert.Equal(1, GetInt64(sourceOne, "events"));
        Assert.Equal(1, GetInt64(sourceOne, "payload.values"));
        Assert.Equal(2, GetInt64(sourceOne, "raw.checksum"));
        Assert.Equal(3, handler.InvocationCount);
    }

    [Fact]
    public void CoreInvokesConfiguredHandlerAndProjectsSnapshots()
    {
        var handler = new CountingHandler();
        var universe = CreateUniverse(sourceCount: 2);
        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(handlers: new IRadarSourceProcessingHandler[] { handler }));
        var batch = CreateBatch(new[] { 0, 1 });

        var result = core.Process(batch);
        var sourceZero = core.GetSourceHandlerSnapshot(sourceId: 0);
        var sourceOne = core.GetSourceHandlerSnapshot(sourceId: 1);

        Assert.True(result.IsValid);
        Assert.Equal(2, handler.InvocationCount);
        Assert.Equal(1, GetInt64(sourceZero, "events"));
        Assert.Equal(1, GetInt64(sourceZero, "payload.values"));
        Assert.Equal(1, GetInt64(sourceZero, "raw.checksum"));
        Assert.Equal(1, GetInt64(sourceOne, "events"));
        Assert.Equal(1, GetInt64(sourceOne, "payload.values"));
        Assert.Equal(2, GetInt64(sourceOne, "raw.checksum"));
        Assert.Equal(1.0, GetDouble(sourceOne, "last.scale"));
    }

    [Fact]
    public void HandlerReceivesEventPayloadSpan()
    {
        var handler = new PayloadCapturingHandler();
        var universe = CreateUniverse(sourceCount: 1);
        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(handlers: new IRadarSourceProcessingHandler[] { handler }));
        var batch = CreateBatch(new[] { 0 });

        var result = core.Process(batch);

        Assert.True(result.IsValid);
        Assert.Equal(1, handler.LastPayloadLength);
        Assert.Equal(1, handler.LastFirstPayloadByte);
        Assert.Equal(1, handler.LastPayloadValueCount);
        Assert.Equal(1, handler.InvocationCount);
    }

    [Fact]
    public void StateStoreRequiresPayloadAwareApplyWhenHandlersAreConfigured()
    {
        var store = new RadarSourceProcessingStateStore(
            CreateUniverse(sourceCount: 1),
            new IRadarSourceProcessingHandler[] { new PayloadCapturingHandler() });
        var batch = CreateBatch(new[] { 0 });

        Assert.Throws<InvalidOperationException>(() =>
            store.ApplyProcessedEvent(
                batch.Events.Span[0],
                processedPayloadValueCount: 1,
                rawValueChecksum: 1));
    }

    [Fact]
    public void NoConfiguredHandlersKeepsBasePathValid()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = new RadarProcessingCore(universe);
        var batch = CreateBatch(new[] { 0 });

        var result = core.Process(batch);
        var handlerSnapshot = core.GetSourceHandlerSnapshot(sourceId: 0);

        Assert.True(result.IsValid);
        Assert.Empty(core.Options.Handlers);
        Assert.Empty(handlerSnapshot.Values);
    }

    private static long GetInt64(
        RadarSourceProcessingHandlerSnapshot snapshot,
        string name)
    {
        Assert.True(snapshot.TryGetValue(name, out var value));
        Assert.Equal(RadarSourceProcessingSnapshotFieldType.Int64, value.Type);
        return value.Int64Value;
    }

    private static double GetDouble(
        RadarSourceProcessingHandlerSnapshot snapshot,
        string name)
    {
        Assert.True(snapshot.TryGetValue(name, out var value));
        Assert.Equal(RadarSourceProcessingSnapshotFieldType.Double, value.Type);
        return value.DoubleValue;
    }

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarEventBatch CreateBatch(int[] sourceIds)
    {
        var builder = new RadarEventBatchBuilder(
            initialEventCapacity: sourceIds.Length,
            initialPayloadCapacity: sourceIds.Length);
        for (var i = 0; i < sourceIds.Length; i++)
        {
            builder.AddEvent(
                CreateIdentity(sourceIds[i]),
                volumeTimestampUtcTicks: 90,
                messageTimestampUtcTicks: 100 + i,
                sourceRecord: 1,
                sourceMessage: 1,
                radialSequence: i,
                gateStart: 0,
                gateCount: 1,
                wordSize: RadarStreamWordSize.EightBit,
                scale: 1.0f,
                offset: 0.0f,
                statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                payload: new byte[] { (byte)(i + 1) });
        }

        return builder.Build();
    }

    private static RadarStreamIdentity CreateIdentity(int sourceId) =>
        new(
            sourceId,
            radarOrdinal: 0,
            momentId: 0,
            elevationSlot: 0,
            azimuthBucket: (ushort)sourceId,
            rangeBand: 0,
            dictionaryVersion: DictionaryVersion.Initial,
            sourceUniverseVersion: SourceUniverseVersion.Initial);

    private static void ApplyEvent(
        RadarSourceProcessingStateStore store,
        RadarEventBatch batch,
        int eventIndex)
    {
        var streamEvent = batch.Events.Span[eventIndex];
        var payload = RadarProcessingPayloadReader.GetEventPayload(streamEvent, batch.Payload.Span);
        var payloadMetrics = RadarProcessingPayloadReader.ComputeEventMetrics(streamEvent, batch.Payload.Span);

        store.ApplyProcessedEvent(
            streamEvent,
            payload,
            payloadMetrics);
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

        public int InvocationCount { get; private set; }

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            InvocationCount++;
            state.AddInt64(slotIndex: 0, value: 1);
            state.AddInt64(slotIndex: 1, context.PayloadMetrics.PayloadValueCount);
            state.AddInt64(slotIndex: 2, context.PayloadMetrics.RawValueChecksum);
            state.SetDouble(slotIndex: 0, context.StreamEvent.Scale);
        }
    }

    private sealed class PayloadCapturingHandler : IRadarSourceProcessingHandler
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "payload",
                int64SlotCount: 0,
                doubleSlotCount: 0);

        public int InvocationCount { get; private set; }

        public int LastPayloadLength { get; private set; }

        public byte LastFirstPayloadByte { get; private set; }

        public long LastPayloadValueCount { get; private set; }

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            InvocationCount++;
            LastPayloadLength = context.Payload.Length;
            LastFirstPayloadByte = context.Payload[0];
            LastPayloadValueCount = context.PayloadMetrics.PayloadValueCount;
        }
    }
}
