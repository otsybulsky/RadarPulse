using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Streaming;

public sealed partial class RadarEventBatchBuilderTests
{
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

}
