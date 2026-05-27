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
    public void LeasedOwnedSnapshotPreservesMetadataEventsPayloadAndMetricsAfterBufferReuse()
    {
        var sourceUniverseVersion = new SourceUniverseVersion(2);
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 2, initialPayloadCapacity: 8);
        builder.AddEvent(
            CreateIdentity(
                sourceId: 7,
                radarOrdinal: 2,
                momentId: 3,
                elevationSlot: 1,
                azimuthBucket: 2,
                rangeBand: 1,
                dictionaryVersion: new DictionaryVersion(4),
                sourceUniverseVersion: sourceUniverseVersion),
            volumeTimestampUtcTicks: 101,
            messageTimestampUtcTicks: 201,
            sourceRecord: 11,
            sourceMessage: 12,
            radialSequence: 13,
            gateStart: 5,
            gateCount: 3,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 0.5f,
            offset: -1.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: [1, 2, 3]);
        builder.AddEvent(
            CreateIdentity(
                sourceId: 8,
                radarOrdinal: 3,
                momentId: 4,
                elevationSlot: 1,
                azimuthBucket: 3,
                rangeBand: 2,
                dictionaryVersion: new DictionaryVersion(6),
                sourceUniverseVersion: sourceUniverseVersion),
            volumeTimestampUtcTicks: 102,
            messageTimestampUtcTicks: 202,
            sourceRecord: 14,
            sourceMessage: 15,
            radialSequence: 16,
            gateStart: 9,
            gateCount: 2,
            wordSize: RadarStreamWordSize.SixteenBit,
            scale: 2.0f,
            offset: 66.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: [0, 4, 0, 5]);

        RadarEventBatch? leasedBatch = null;
        RadarEventBatch? retainedSnapshot = null;
        builder.ConsumeLeased(batch =>
        {
            leasedBatch = batch;
            retainedSnapshot = batch.ToOwnedSnapshot();
        });

        Assert.NotNull(leasedBatch);
        Assert.NotNull(retainedSnapshot);
        Assert.NotSame(leasedBatch, retainedSnapshot);
        Assert.Equal(RadarEventBatchLifetime.Leased, leasedBatch.Lifetime);
        Assert.Equal(RadarEventBatchLifetime.Owned, retainedSnapshot.Lifetime);
        Assert.Equal(StreamSchemaVersion.Current, retainedSnapshot.StreamSchemaVersion);
        Assert.Equal(new DictionaryVersion(6), retainedSnapshot.DictionaryVersion);
        Assert.Equal(sourceUniverseVersion, retainedSnapshot.SourceUniverseVersion);
        Assert.Equal(2, retainedSnapshot.EventCount);
        Assert.Equal(7, retainedSnapshot.PayloadLength);
        Assert.Equal([1, 2, 3, 0, 4, 0, 5], retainedSnapshot.Payload.ToArray());
        Assert.True(retainedSnapshot.TryGetPayloadMetrics(out var payloadValueCount, out var rawValueChecksum));
        Assert.Equal(5, payloadValueCount);
        Assert.Equal(15, rawValueChecksum);

        var firstEvent = retainedSnapshot.Events.Span[0];
        Assert.Equal(7, firstEvent.SourceId);
        Assert.Equal(2, firstEvent.RadarOrdinal);
        Assert.Equal(101, firstEvent.VolumeTimestampUtcTicks);
        Assert.Equal(201, firstEvent.MessageTimestampUtcTicks);
        Assert.Equal(11, firstEvent.SourceRecord);
        Assert.Equal(12, firstEvent.SourceMessage);
        Assert.Equal(13, firstEvent.RadialSequence);
        Assert.Equal(1, firstEvent.ElevationSlot);
        Assert.Equal(2, firstEvent.AzimuthBucket);
        Assert.Equal(1, firstEvent.RangeBand);
        Assert.Equal(3, firstEvent.MomentId);
        Assert.Equal(5, firstEvent.GateStart);
        Assert.Equal(3, firstEvent.GateCount);
        Assert.Equal(RadarStreamWordSize.EightBit, firstEvent.WordSize);
        Assert.Equal(0.5f, firstEvent.Scale);
        Assert.Equal(-1.0f, firstEvent.Offset);
        Assert.Equal(RadarStreamStatusModel.ArchiveTwoMoment, firstEvent.StatusModel);
        Assert.Equal(0, firstEvent.PayloadOffset);
        Assert.Equal(3, firstEvent.PayloadLength);

        var secondEvent = retainedSnapshot.Events.Span[1];
        Assert.Equal(8, secondEvent.SourceId);
        Assert.Equal(3, secondEvent.RadarOrdinal);
        Assert.Equal(102, secondEvent.VolumeTimestampUtcTicks);
        Assert.Equal(202, secondEvent.MessageTimestampUtcTicks);
        Assert.Equal(14, secondEvent.SourceRecord);
        Assert.Equal(15, secondEvent.SourceMessage);
        Assert.Equal(16, secondEvent.RadialSequence);
        Assert.Equal(1, secondEvent.ElevationSlot);
        Assert.Equal(3, secondEvent.AzimuthBucket);
        Assert.Equal(2, secondEvent.RangeBand);
        Assert.Equal(4, secondEvent.MomentId);
        Assert.Equal(9, secondEvent.GateStart);
        Assert.Equal(2, secondEvent.GateCount);
        Assert.Equal(RadarStreamWordSize.SixteenBit, secondEvent.WordSize);
        Assert.Equal(2.0f, secondEvent.Scale);
        Assert.Equal(66.0f, secondEvent.Offset);
        Assert.Equal(RadarStreamStatusModel.ArchiveTwoMoment, secondEvent.StatusModel);
        Assert.Equal(3, secondEvent.PayloadOffset);
        Assert.Equal(4, secondEvent.PayloadLength);

        builder.AddEvent(
            CreateIdentity(sourceId: 99, sourceUniverseVersion: sourceUniverseVersion),
            volumeTimestampUtcTicks: 301,
            messageTimestampUtcTicks: 401,
            sourceRecord: 21,
            sourceMessage: 22,
            radialSequence: 23,
            gateStart: 0,
            gateCount: 1,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: [99]);
        var reusedBatch = builder.Build();

        Assert.Equal([99], reusedBatch.Payload.ToArray());
        Assert.Equal(99, reusedBatch.Events.Span[0].SourceId);
        Assert.Equal([1, 2, 3, 0, 4, 0, 5], retainedSnapshot.Payload.ToArray());
        Assert.Equal(7, retainedSnapshot.Events.Span[0].SourceId);
        Assert.Equal(8, retainedSnapshot.Events.Span[1].SourceId);
        Assert.True(retainedSnapshot.TryGetPayloadMetrics(out payloadValueCount, out rawValueChecksum));
        Assert.Equal(5, payloadValueCount);
        Assert.Equal(15, rawValueChecksum);
    }

    [Fact]
    public void EmptyLeasedOwnedSnapshotIsOwnedAndCarriesEmptyMetrics()
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 0, initialPayloadCapacity: 0);

        RadarEventBatch? retainedSnapshot = null;
        builder.ConsumeLeased(batch =>
        {
            Assert.Equal(RadarEventBatchLifetime.Leased, batch.Lifetime);
            Assert.Equal(0, batch.EventCount);
            Assert.Equal(0, batch.PayloadLength);
            retainedSnapshot = batch.ToOwnedSnapshot();
        });

        Assert.NotNull(retainedSnapshot);
        Assert.Equal(RadarEventBatchLifetime.Owned, retainedSnapshot.Lifetime);
        Assert.Equal(StreamSchemaVersion.Current, retainedSnapshot.StreamSchemaVersion);
        Assert.Equal(DictionaryVersion.Initial, retainedSnapshot.DictionaryVersion);
        Assert.Equal(SourceUniverseVersion.Initial, retainedSnapshot.SourceUniverseVersion);
        Assert.Equal(0, retainedSnapshot.EventCount);
        Assert.Equal(0, retainedSnapshot.PayloadLength);
        Assert.Empty(retainedSnapshot.Events.ToArray());
        Assert.Empty(retainedSnapshot.Payload.ToArray());
        Assert.True(retainedSnapshot.TryGetPayloadMetrics(out var payloadValueCount, out var rawValueChecksum));
        Assert.Equal(0, payloadValueCount);
        Assert.Equal(0, rawValueChecksum);
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
