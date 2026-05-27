using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingStateHandoffValidatorTests
{
    [Fact]
    public void ValidatePassesWhenOnlyOwnerShardChanges()
    {
        var store = CreatePopulatedStore();
        var before = Capture(store, partitionId: 0, shardId: 0, sourceIdStart: 0, sourceIdEndExclusive: 4);
        var after = Capture(store, partitionId: 0, shardId: 1, sourceIdStart: 0, sourceIdEndExclusive: 4);

        var result = RadarProcessingStateHandoffValidator.Validate(before, after);

        Assert.True(result.IsValid);
        Assert.Equal(RadarProcessingStateHandoffValidationError.None, result.Error);
        Assert.NotEqual(before.ShardId, after.ShardId);
        Assert.Same(before, result.BeforeSnapshot);
        Assert.Same(after, result.AfterSnapshot);
    }

    [Fact]
    public void ValidateDetectsActiveSourceCountMismatch()
    {
        var before = Capture(CreatePopulatedStore(), partitionId: 0, shardId: 0, sourceIdStart: 0, sourceIdEndExclusive: 4);
        var after = Copy(before, activeSourceCount: before.ActiveSourceCount - 1);

        var result = RadarProcessingStateHandoffValidator.Validate(before, after);

        AssertInvalid(result, RadarProcessingStateHandoffValidationError.ActiveSourceCountMismatch);
    }

    [Fact]
    public void ValidateDetectsProcessedEventCountMismatch()
    {
        var before = Capture(CreatePopulatedStore(), partitionId: 0, shardId: 0, sourceIdStart: 0, sourceIdEndExclusive: 4);
        var after = Copy(before, processedEventCount: before.ProcessedEventCount + 1);

        var result = RadarProcessingStateHandoffValidator.Validate(before, after);

        AssertInvalid(result, RadarProcessingStateHandoffValidationError.ProcessedEventCountMismatch);
    }

    [Fact]
    public void ValidateDetectsProcessedPayloadValueCountMismatch()
    {
        var before = Capture(CreatePopulatedStore(), partitionId: 0, shardId: 0, sourceIdStart: 0, sourceIdEndExclusive: 4);
        var after = Copy(before, processedPayloadValueCount: before.ProcessedPayloadValueCount + 1);

        var result = RadarProcessingStateHandoffValidator.Validate(before, after);

        AssertInvalid(result, RadarProcessingStateHandoffValidationError.ProcessedPayloadValueCountMismatch);
    }

    [Fact]
    public void ValidateDetectsRawChecksumMismatch()
    {
        var before = Capture(CreatePopulatedStore(), partitionId: 0, shardId: 0, sourceIdStart: 0, sourceIdEndExclusive: 4);
        var after = Copy(before, rawValueChecksum: before.RawValueChecksum + 1);

        var result = RadarProcessingStateHandoffValidator.Validate(before, after);

        AssertInvalid(result, RadarProcessingStateHandoffValidationError.RawValueChecksumMismatch);
    }

    [Fact]
    public void ValidateDetectsProcessingChecksumMismatch()
    {
        var before = Capture(CreatePopulatedStore(), partitionId: 0, shardId: 0, sourceIdStart: 0, sourceIdEndExclusive: 4);
        var after = Copy(
            before,
            checksum: before.Checksum with
            {
                ProcessingChecksum = before.Checksum.ProcessingChecksum ^ 1UL
            });

        var result = RadarProcessingStateHandoffValidator.Validate(before, after);

        AssertInvalid(result, RadarProcessingStateHandoffValidationError.ProcessingChecksumMismatch);
    }

    [Fact]
    public void ValidateDetectsLastMessageTimestampChecksumMismatch()
    {
        var before = Capture(CreatePopulatedStore(), partitionId: 0, shardId: 0, sourceIdStart: 0, sourceIdEndExclusive: 4);
        var after = Copy(
            before,
            checksum: before.Checksum with
            {
                LastMessageTimestampChecksum = before.Checksum.LastMessageTimestampChecksum ^ 1UL
            });

        var result = RadarProcessingStateHandoffValidator.Validate(before, after);

        AssertInvalid(result, RadarProcessingStateHandoffValidationError.LastMessageTimestampChecksumMismatch);
    }

    [Fact]
    public void HandlerSlotChecksumParticipatesWhenHandlersExist()
    {
        var store = new RadarSourceProcessingStateStore(
            CreateUniverse(sourceCount: 2),
            new IRadarSourceProcessingHandler[] { new CountingHandler() });
        store.ApplyProcessedEvent(
            CreateEvent(sourceId: 0, messageTimestampUtcTicks: 100),
            new byte[] { 1 },
            new RadarProcessingPayloadMetrics(PayloadValueCount: 1, RawValueChecksum: 1));
        store.ApplyProcessedEvent(
            CreateEvent(sourceId: 1, messageTimestampUtcTicks: 101),
            new byte[] { 2 },
            new RadarProcessingPayloadMetrics(PayloadValueCount: 1, RawValueChecksum: 2));

        var before = Capture(store, partitionId: 0, shardId: 0, sourceIdStart: 0, sourceIdEndExclusive: 2);
        var after = Copy(
            before,
            shardId: 1,
            checksum: before.Checksum with
            {
                HandlerSnapshotChecksum = before.Checksum.HandlerSnapshotChecksum ^ 1UL
            });

        var result = RadarProcessingStateHandoffValidator.Validate(before, after);

        Assert.NotEqual(0UL, before.Checksum.HandlerSnapshotChecksum);
        AssertInvalid(result, RadarProcessingStateHandoffValidationError.HandlerSnapshotChecksumMismatch);
    }

    [Fact]
    public void EmptyPartitionStateHandoffIsValid()
    {
        var store = CreateStore(sourceCount: 4);
        var before = Capture(store, partitionId: 1, shardId: 0, sourceIdStart: 2, sourceIdEndExclusive: 4);
        var after = Capture(store, partitionId: 1, shardId: 2, sourceIdStart: 2, sourceIdEndExclusive: 4);

        var result = RadarProcessingStateHandoffValidator.Validate(before, after);

        Assert.True(result.IsValid);
        Assert.Equal(0, before.ActiveSourceCount);
        Assert.Equal(0, before.ProcessedEventCount);
        Assert.Equal(RadarProcessingPartitionStateChecksum.Empty, before.Checksum);
    }

    private static RadarProcessingPartitionStateSnapshot Capture(
        RadarSourceProcessingStateStore store,
        int partitionId,
        int shardId,
        int sourceIdStart,
        int sourceIdEndExclusive) =>
        RadarProcessingPartitionStateSnapshot.Capture(
            new RadarProcessingPartitionAssignment(
                partitionId,
                shardId,
                sourceIdStart,
                sourceIdEndExclusive),
            store);

    private static RadarProcessingPartitionStateSnapshot Copy(
        RadarProcessingPartitionStateSnapshot snapshot,
        int? shardId = null,
        long? activeSourceCount = null,
        long? processedEventCount = null,
        long? processedPayloadValueCount = null,
        long? rawValueChecksum = null,
        RadarProcessingPartitionStateChecksum? checksum = null) =>
        new(
            snapshot.PartitionId,
            shardId ?? snapshot.ShardId,
            snapshot.SourceIdStart,
            snapshot.SourceIdEndExclusive,
            activeSourceCount ?? snapshot.ActiveSourceCount,
            processedEventCount ?? snapshot.ProcessedEventCount,
            processedPayloadValueCount ?? snapshot.ProcessedPayloadValueCount,
            rawValueChecksum ?? snapshot.RawValueChecksum,
            checksum ?? snapshot.Checksum);

    private static void AssertInvalid(
        RadarProcessingStateHandoffValidationResult result,
        RadarProcessingStateHandoffValidationError expectedError)
    {
        Assert.False(result.IsValid);
        Assert.Equal(expectedError, result.Error);
    }

    private static RadarSourceProcessingStateStore CreatePopulatedStore()
    {
        var store = CreateStore(sourceCount: 4);
        store.ApplyProcessedEvent(
            CreateEvent(sourceId: 0, messageTimestampUtcTicks: 100),
            processedPayloadValueCount: 4,
            rawValueChecksum: 10);
        store.ApplyProcessedEvent(
            CreateEvent(sourceId: 2, messageTimestampUtcTicks: 101),
            processedPayloadValueCount: 6,
            rawValueChecksum: 20);
        store.ApplyProcessedEvent(
            CreateEvent(sourceId: 2, messageTimestampUtcTicks: 102),
            processedPayloadValueCount: 8,
            rawValueChecksum: 30);

        return store;
    }

    private static RadarSourceProcessingStateStore CreateStore(int sourceCount) =>
        new(CreateUniverse(sourceCount));

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarStreamEvent CreateEvent(
        int sourceId,
        long messageTimestampUtcTicks) =>
        new(
            sourceId,
            radarOrdinal: 0,
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks,
            sourceRecord: 1,
            sourceMessage: 1,
            radialSequence: 1,
            elevationSlot: 0,
            azimuthBucket: (ushort)sourceId,
            rangeBand: 0,
            momentId: 0,
            gateStart: 0,
            gateCount: 1,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payloadOffset: 0,
            payloadLength: 1);

    private sealed class CountingHandler : IRadarSourceProcessingHandler
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "counting",
                int64SlotCount: 3,
                doubleSlotCount: 1,
                new[]
                {
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "events",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 0),
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "payload.values",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 1),
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "raw.checksum",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 2),
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "last.scale",
                        RadarSourceProcessingSnapshotFieldType.Double,
                        slotIndex: 0)
                });

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
