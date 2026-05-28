using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Streaming;

public sealed partial class RadarEventBatchBuilderTests
{
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

}
