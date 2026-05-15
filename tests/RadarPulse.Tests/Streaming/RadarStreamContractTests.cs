using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Streaming;

public sealed class RadarStreamContractTests
{
    [Fact]
    public void RadarStreamEventUsesOneCacheLineStride()
    {
        Assert.Equal(64, RadarStreamEvent.SizeInBytes);
        Assert.Equal(RadarStreamEvent.SizeInBytes, Marshal.SizeOf<RadarStreamEvent>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<RadarStreamEvent>());
    }

    [Fact]
    public void RadarStreamEventPreservesExplicitFields()
    {
        var streamEvent = CreateEvent(
            sourceId: 42,
            payloadOffset: 4,
            gateCount: 2,
            wordSize: RadarStreamWordSize.SixteenBit);

        Assert.Equal(42, streamEvent.SourceId);
        Assert.Equal(7, streamEvent.RadarOrdinal);
        Assert.Equal(638_828_640_000_000_000L, streamEvent.VolumeTimestampUtcTicks);
        Assert.Equal(638_828_640_120_000_000L, streamEvent.MessageTimestampUtcTicks);
        Assert.Equal(11, streamEvent.SourceRecord);
        Assert.Equal(3, streamEvent.SourceMessage);
        Assert.Equal(720, streamEvent.RadialSequence);
        Assert.Equal(2, streamEvent.ElevationSlot);
        Assert.Equal(180, streamEvent.AzimuthBucket);
        Assert.Equal(5, streamEvent.RangeBand);
        Assert.Equal(4, streamEvent.MomentId);
        Assert.Equal(120, streamEvent.GateStart);
        Assert.Equal(2, streamEvent.GateCount);
        Assert.Equal(RadarStreamWordSize.SixteenBit, streamEvent.WordSize);
        Assert.Equal(2, streamEvent.BytesPerGate);
        Assert.Equal(4, streamEvent.ExpectedPayloadLength);
        Assert.Equal(2.0f, streamEvent.Scale);
        Assert.Equal(66.0f, streamEvent.Offset);
        Assert.Equal(RadarStreamStatusModel.ArchiveTwoMoment, streamEvent.StatusModel);
        Assert.Equal(4, streamEvent.PayloadOffset);
        Assert.Equal(4, streamEvent.PayloadLength);
    }

    [Fact]
    public void RadarEventBatchCarriesVersionsEventsAndPayload()
    {
        var events = new[]
        {
            CreateEvent(sourceId: 1, payloadOffset: 0, gateCount: 4, wordSize: RadarStreamWordSize.EightBit),
            CreateEvent(sourceId: 2, payloadOffset: 4, gateCount: 2, wordSize: RadarStreamWordSize.SixteenBit)
        };
        var payload = new byte[8];
        var batch = new RadarEventBatch(
            StreamSchemaVersion.Current,
            new DictionaryVersion(5),
            new SourceUniverseVersion(2),
            events,
            payload);

        Assert.Equal(StreamSchemaVersion.Current, batch.StreamSchemaVersion);
        Assert.Equal(new DictionaryVersion(5), batch.DictionaryVersion);
        Assert.Equal(new SourceUniverseVersion(2), batch.SourceUniverseVersion);
        Assert.Equal(2, batch.EventCount);
        Assert.Equal(8, batch.PayloadLength);
        Assert.Equal(events, batch.Events.ToArray());
        Assert.Equal(payload, batch.Payload.ToArray());
        Assert.False(batch.TryGetPayloadMetrics(out _, out _));
    }

    [Fact]
    public void RadarEventBatchRejectsPayloadLengthMismatch()
    {
        var events = new[]
        {
            new RadarStreamEvent(
                sourceId: 1,
                radarOrdinal: 7,
                volumeTimestampUtcTicks: 638_828_640_000_000_000L,
                messageTimestampUtcTicks: 638_828_640_120_000_000L,
                sourceRecord: 11,
                sourceMessage: 3,
                radialSequence: 720,
                elevationSlot: 2,
                azimuthBucket: 180,
                rangeBand: 5,
                momentId: 4,
                gateStart: 120,
                gateCount: 2,
                wordSize: RadarStreamWordSize.SixteenBit,
                scale: 2.0f,
                offset: 66.0f,
                statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                payloadOffset: 0,
                payloadLength: 2)
        };

        Assert.Throws<ArgumentException>(() => new RadarEventBatch(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            SourceUniverseVersion.Initial,
            events,
            new byte[4]));
    }

    [Fact]
    public void RadarEventBatchRejectsPayloadReferenceOutsideBatchPayload()
    {
        var events = new[]
        {
            CreateEvent(sourceId: 1, payloadOffset: 6, gateCount: 2, wordSize: RadarStreamWordSize.SixteenBit)
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarEventBatch(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            SourceUniverseVersion.Initial,
            events,
            new byte[8]));
    }

    [Fact]
    public void VersionValuesRejectZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StreamSchemaVersion(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DictionaryVersion(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SourceUniverseVersion(0));
    }

    private static RadarStreamEvent CreateEvent(
        int sourceId,
        int payloadOffset,
        ushort gateCount,
        RadarStreamWordSize wordSize)
    {
        var payloadLength = checked(gateCount * (wordSize == RadarStreamWordSize.EightBit ? 1 : 2));
        return new RadarStreamEvent(
            sourceId,
            radarOrdinal: 7,
            volumeTimestampUtcTicks: 638_828_640_000_000_000L,
            messageTimestampUtcTicks: 638_828_640_120_000_000L,
            sourceRecord: 11,
            sourceMessage: 3,
            radialSequence: 720,
            elevationSlot: 2,
            azimuthBucket: 180,
            rangeBand: 5,
            momentId: 4,
            gateStart: 120,
            gateCount: gateCount,
            wordSize: wordSize,
            scale: 2.0f,
            offset: 66.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payloadOffset: payloadOffset,
            payloadLength: payloadLength);
    }
}
