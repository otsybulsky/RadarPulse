using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Streaming;

public sealed class RadarEventBatchBuilderTests
{
    [Fact]
    public void AddEventCopiesPayloadAndSetsEventPayloadReference()
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: 2);
        byte[] payload = [10, 20, 30, 40];
        var identity = CreateIdentity(sourceId: 42);

        var streamEvent = builder.AddEvent(
            identity,
            volumeTimestampUtcTicks: 638_828_640_000_000_000L,
            messageTimestampUtcTicks: 638_828_640_120_000_000L,
            sourceRecord: 11,
            sourceMessage: 3,
            radialSequence: 720,
            gateStart: 120,
            gateCount: 4,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 2.0f,
            offset: 66.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: payload);
        payload[0] = 255;
        var batch = builder.Build();

        Assert.Equal(1, builder.EventCount);
        Assert.Equal(4, builder.PayloadLength);
        Assert.Equal(42, streamEvent.SourceId);
        Assert.Equal(0, streamEvent.PayloadOffset);
        Assert.Equal(4, streamEvent.PayloadLength);
        Assert.Equal(1, batch.EventCount);
        Assert.Equal(4, batch.PayloadLength);
        Assert.Equal([10, 20, 30, 40], batch.Payload.ToArray());
        Assert.True(batch.TryGetPayloadMetrics(out var payloadValueCount, out var rawValueChecksum));
        Assert.Equal(4, payloadValueCount);
        Assert.Equal(100, rawValueChecksum);
        Assert.Equal(streamEvent, batch.Events.Span[0]);
    }

    [Fact]
    public void MultipleEventsAppendPayloadSequentiallyAndKeepEventOrder()
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 2, initialPayloadCapacity: 0);
        var firstIdentity = CreateIdentity(sourceId: 17, rangeBand: 0, dictionaryVersion: new DictionaryVersion(3));
        var secondIdentity = CreateIdentity(sourceId: 42, rangeBand: 1, dictionaryVersion: new DictionaryVersion(5));

        builder.AddEvent(
            firstIdentity,
            1,
            2,
            sourceRecord: 0,
            sourceMessage: 0,
            radialSequence: 0,
            gateStart: 0,
            gateCount: 2,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: [1, 2]);
        builder.AddEvent(
            secondIdentity,
            3,
            4,
            sourceRecord: 1,
            sourceMessage: 0,
            radialSequence: 1,
            gateStart: 10,
            gateCount: 2,
            wordSize: RadarStreamWordSize.SixteenBit,
            scale: 2.0f,
            offset: 66.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: [3, 4, 5, 6]);
        var batch = builder.Build();

        Assert.Equal(new DictionaryVersion(5), batch.DictionaryVersion);
        Assert.Equal(SourceUniverseVersion.Initial, batch.SourceUniverseVersion);
        Assert.Equal([1, 2, 3, 4, 5, 6], batch.Payload.ToArray());
        Assert.Equal(17, batch.Events.Span[0].SourceId);
        Assert.Equal(42, batch.Events.Span[1].SourceId);
        Assert.Equal(0, batch.Events.Span[0].PayloadOffset);
        Assert.Equal(2, batch.Events.Span[0].PayloadLength);
        Assert.Equal(2, batch.Events.Span[1].PayloadOffset);
        Assert.Equal(4, batch.Events.Span[1].PayloadLength);
    }

    [Fact]
    public void BuildReturnsOwnedSnapshotsThatDoNotChangeAfterLaterAppends()
    {
        var builder = new RadarEventBatchBuilder();
        builder.AddEvent(
            CreateIdentity(sourceId: 1),
            1,
            2,
            sourceRecord: 0,
            sourceMessage: 0,
            radialSequence: 0,
            gateStart: 0,
            gateCount: 2,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: [7, 8]);
        var firstBatch = builder.Build();

        builder.AddEvent(
            CreateIdentity(sourceId: 2, rangeBand: 1, dictionaryVersion: new DictionaryVersion(4)),
            3,
            4,
            sourceRecord: 1,
            sourceMessage: 0,
            radialSequence: 1,
            gateStart: 2,
            gateCount: 1,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: [9]);
        var secondBatch = builder.Build();

        Assert.Equal(1, firstBatch.EventCount);
        Assert.Equal(2, firstBatch.PayloadLength);
        Assert.Equal([7, 8], firstBatch.Payload.ToArray());
        Assert.Equal(2, secondBatch.EventCount);
        Assert.Equal([7, 8, 9], secondBatch.Payload.ToArray());
    }

    [Fact]
    public void BuildAndResetTransfersCurrentPayloadAndAllowsReuse()
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 2, initialPayloadCapacity: 16);
        builder.AddEvent(
            CreateIdentity(sourceId: 1, dictionaryVersion: new DictionaryVersion(4)),
            1,
            2,
            sourceRecord: 0,
            sourceMessage: 0,
            radialSequence: 0,
            gateStart: 0,
            gateCount: 2,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: [7, 8]);

        var firstBatch = builder.BuildAndReset();

        Assert.Equal(0, builder.EventCount);
        Assert.Equal(0, builder.PayloadLength);
        Assert.Equal(1, firstBatch.EventCount);
        Assert.Equal(2, firstBatch.PayloadLength);
        Assert.Equal(RadarEventBatchLifetime.Owned, firstBatch.Lifetime);
        Assert.Equal(new DictionaryVersion(4), firstBatch.DictionaryVersion);
        Assert.Equal([7, 8], firstBatch.Payload.ToArray());
        Assert.True(firstBatch.TryGetPayloadMetrics(out var firstPayloadValueCount, out var firstRawValueChecksum));
        Assert.Equal(2, firstPayloadValueCount);
        Assert.Equal(15, firstRawValueChecksum);

        builder.AddEvent(
            CreateIdentity(sourceId: 2, rangeBand: 1, dictionaryVersion: new DictionaryVersion(5)),
            3,
            4,
            sourceRecord: 1,
            sourceMessage: 0,
            radialSequence: 1,
            gateStart: 0,
            gateCount: 1,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: [9]);

        var secondBatch = builder.BuildAndReset();

        Assert.Equal([7, 8], firstBatch.Payload.ToArray());
        Assert.Equal(1, secondBatch.EventCount);
        Assert.Equal(RadarEventBatchLifetime.Owned, secondBatch.Lifetime);
        Assert.Equal([9], secondBatch.Payload.ToArray());
        Assert.Equal(new DictionaryVersion(5), secondBatch.DictionaryVersion);
        Assert.True(secondBatch.TryGetPayloadMetrics(out var secondPayloadValueCount, out var secondRawValueChecksum));
        Assert.Equal(1, secondPayloadValueCount);
        Assert.Equal(9, secondRawValueChecksum);
    }

    [Fact]
    public void ConsumeLeasedPublishesBorrowedBatchAndAllowsExplicitOwnedSnapshot()
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: 2);
        builder.AddEvent(
            CreateIdentity(sourceId: 1, dictionaryVersion: new DictionaryVersion(4)),
            1,
            2,
            sourceRecord: 0,
            sourceMessage: 0,
            radialSequence: 0,
            gateStart: 0,
            gateCount: 2,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: [7, 8]);

        RadarEventBatch? retainedSnapshot = null;
        builder.ConsumeLeased(batch =>
        {
            Assert.Equal(RadarEventBatchLifetime.Leased, batch.Lifetime);
            Assert.Equal(1, batch.EventCount);
            Assert.Equal([7, 8], batch.Payload.ToArray());
            retainedSnapshot = batch.ToOwnedSnapshot();
        });

        Assert.Equal(0, builder.EventCount);
        Assert.Equal(0, builder.PayloadLength);
        Assert.NotNull(retainedSnapshot);
        Assert.Equal(RadarEventBatchLifetime.Owned, retainedSnapshot.Lifetime);
        Assert.Equal([7, 8], retainedSnapshot.Payload.ToArray());

        builder.AddEvent(
            CreateIdentity(sourceId: 2, rangeBand: 1, dictionaryVersion: new DictionaryVersion(5)),
            3,
            4,
            sourceRecord: 1,
            sourceMessage: 0,
            radialSequence: 1,
            gateStart: 0,
            gateCount: 1,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: [9]);

        var secondBatch = builder.Build();

        Assert.Equal([7, 8], retainedSnapshot.Payload.ToArray());
        Assert.Equal([9], secondBatch.Payload.ToArray());
    }

    [Fact]
    public void EmptyBuilderBuildsEmptyBatchWithInitialVersions()
    {
        var batch = new RadarEventBatchBuilder().Build();

        Assert.Equal(StreamSchemaVersion.Current, batch.StreamSchemaVersion);
        Assert.Equal(DictionaryVersion.Initial, batch.DictionaryVersion);
        Assert.Equal(SourceUniverseVersion.Initial, batch.SourceUniverseVersion);
        Assert.Equal(0, batch.EventCount);
        Assert.Equal(0, batch.PayloadLength);
    }

    [Fact]
    public void InvalidPayloadLengthDoesNotMutateBuilder()
    {
        var builder = new RadarEventBatchBuilder();

        Assert.Throws<ArgumentException>(() => builder.AddEvent(
            CreateIdentity(sourceId: 1),
            1,
            2,
            sourceRecord: 0,
            sourceMessage: 0,
            radialSequence: 0,
            gateStart: 0,
            gateCount: 2,
            wordSize: RadarStreamWordSize.SixteenBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: [1, 2]));

        Assert.Equal(0, builder.EventCount);
        Assert.Equal(0, builder.PayloadLength);
        var batch = builder.Build();
        Assert.Equal(DictionaryVersion.Initial, batch.DictionaryVersion);
        Assert.Equal(SourceUniverseVersion.Initial, batch.SourceUniverseVersion);
    }

    [Fact]
    public void SourceUniverseVersionMustBeSameWithinOneBatch()
    {
        var builder = new RadarEventBatchBuilder();
        builder.AddEvent(
            CreateIdentity(sourceId: 1, sourceUniverseVersion: SourceUniverseVersion.Initial),
            1,
            2,
            sourceRecord: 0,
            sourceMessage: 0,
            radialSequence: 0,
            gateStart: 0,
            gateCount: 1,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: [1]);

        Assert.Throws<ArgumentException>(() => builder.AddEvent(
            CreateIdentity(sourceId: 2, sourceUniverseVersion: new SourceUniverseVersion(2)),
            3,
            4,
            sourceRecord: 1,
            sourceMessage: 0,
            radialSequence: 1,
            gateStart: 0,
            gateCount: 1,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: [2]));

        var batch = builder.Build();
        Assert.Equal(1, batch.EventCount);
        Assert.Equal([1], batch.Payload.ToArray());
    }

    [Fact]
    public void InvalidIdentityVersionsAreRejected()
    {
        var builder = new RadarEventBatchBuilder();
        var identity = new RadarStreamIdentity(
            sourceId: 1,
            radarOrdinal: 0,
            momentId: 0,
            elevationSlot: 0,
            azimuthBucket: 0,
            rangeBand: 0,
            dictionaryVersion: default,
            sourceUniverseVersion: SourceUniverseVersion.Initial);

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddEvent(
            identity,
            1,
            2,
            sourceRecord: 0,
            sourceMessage: 0,
            radialSequence: 0,
            gateStart: 0,
            gateCount: 1,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: [1]));
    }

    private static RadarStreamIdentity CreateIdentity(
        int sourceId,
        ushort radarOrdinal = 0,
        ushort momentId = 0,
        ushort elevationSlot = 0,
        ushort azimuthBucket = 0,
        ushort rangeBand = 0,
        DictionaryVersion? dictionaryVersion = null,
        SourceUniverseVersion? sourceUniverseVersion = null) =>
        new(
            sourceId,
            radarOrdinal,
            momentId,
            elevationSlot,
            azimuthBucket,
            rangeBand,
            dictionaryVersion ?? new DictionaryVersion(3),
            sourceUniverseVersion ?? SourceUniverseVersion.Initial);
}
