using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Streaming;

public sealed partial class RadarEventBatchBuilderTests
{
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

}
