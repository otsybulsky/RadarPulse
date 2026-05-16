using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingPayloadReaderTests
{
    [Fact]
    public void ComputeEventMetricsReadsEightBitPayloadValues()
    {
        byte[] payload = [9, 1, 2, 3, 4, 8];
        var streamEvent = CreateEvent(
            sourceId: 0,
            payloadOffset: 1,
            gateCount: 4,
            wordSize: RadarStreamWordSize.EightBit);

        var eventPayload = RadarProcessingPayloadReader.GetEventPayload(streamEvent, payload);
        var metrics = RadarProcessingPayloadReader.ComputeEventMetrics(streamEvent, payload);

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, eventPayload.ToArray());
        Assert.Equal(4, metrics.PayloadValueCount);
        Assert.Equal(10, metrics.RawValueChecksum);
    }

    [Fact]
    public void ComputeEventMetricsReadsSixteenBitPayloadValuesAsBigEndian()
    {
        byte[] payload = [0, 1, 1, 0, 0x12, 0x34];
        var streamEvent = CreateEvent(
            sourceId: 0,
            payloadOffset: 0,
            gateCount: 3,
            wordSize: RadarStreamWordSize.SixteenBit);

        var metrics = RadarProcessingPayloadReader.ComputeEventMetrics(streamEvent, payload);

        Assert.Equal(3, metrics.PayloadValueCount);
        Assert.Equal(4_917L, metrics.RawValueChecksum);
    }

    [Fact]
    public void ComputeBatchMetricsMatchesRadarEventBatchMetricsPayloadContract()
    {
        var events = new[]
        {
            CreateEvent(
                sourceId: 0,
                payloadOffset: 0,
                gateCount: 4,
                wordSize: RadarStreamWordSize.EightBit),
            CreateEvent(
                sourceId: 1,
                payloadOffset: 4,
                gateCount: 2,
                wordSize: RadarStreamWordSize.SixteenBit)
        };
        byte[] payload = [1, 2, 3, 4, 0, 5, 1, 0];
        var batch = new RadarEventBatch(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            SourceUniverseVersion.Initial,
            events,
            payload);

        var processingMetrics = RadarProcessingPayloadReader.ComputeBatchMetrics(batch);
        var batchMetrics = RadarEventBatchMetrics.Compute(batch);

        Assert.Equal(batchMetrics.PayloadValueCount, processingMetrics.PayloadValueCount);
        Assert.Equal(batchMetrics.RawValueChecksum, processingMetrics.RawValueChecksum);
    }

    [Fact]
    public void ComputeBatchMetricsReturnsEmptyForEmptyBatch()
    {
        var batch = new RadarEventBatch(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            SourceUniverseVersion.Initial,
            Array.Empty<RadarStreamEvent>(),
            Array.Empty<byte>());

        var metrics = RadarProcessingPayloadReader.ComputeBatchMetrics(batch);

        Assert.Equal(RadarProcessingPayloadMetrics.Empty, metrics);
    }

    [Fact]
    public void PayloadMetricsCanBeAdded()
    {
        var first = new RadarProcessingPayloadMetrics(3, 10);
        var second = new RadarProcessingPayloadMetrics(5, 20);

        var sum = first.Add(second);

        Assert.Equal(8, sum.PayloadValueCount);
        Assert.Equal(30, sum.RawValueChecksum);
    }

    [Fact]
    public void ComputeBatchMetricsRejectsNullBatch()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingPayloadReader.ComputeBatchMetrics(null!));
    }

    [Fact]
    public void GetEventPayloadRejectsPayloadLengthMismatch()
    {
        byte[] payload = [1, 2, 3, 4];
        var streamEvent = CreateEvent(
            sourceId: 0,
            payloadOffset: 0,
            gateCount: 4,
            wordSize: RadarStreamWordSize.EightBit,
            payloadLength: 3);

        Assert.Throws<ArgumentException>(() =>
            RadarProcessingPayloadReader.GetEventPayload(streamEvent, payload));
    }

    [Fact]
    public void GetEventPayloadRejectsPayloadReferenceOutsideBatchPayload()
    {
        byte[] payload = [1, 2, 3];
        var streamEvent = CreateEvent(
            sourceId: 0,
            payloadOffset: 0,
            gateCount: 4,
            wordSize: RadarStreamWordSize.EightBit);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingPayloadReader.GetEventPayload(streamEvent, payload));
    }

    [Fact]
    public void GetEventPayloadRejectsUnsupportedWordSize()
    {
        var streamEvent = default(RadarStreamEvent);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingPayloadReader.GetEventPayload(streamEvent, Array.Empty<byte>()));
    }

    private static RadarStreamEvent CreateEvent(
        int sourceId,
        int payloadOffset,
        ushort gateCount,
        RadarStreamWordSize wordSize,
        int? payloadLength = null)
    {
        var resolvedPayloadLength = payloadLength ??
                                    checked(gateCount * (wordSize == RadarStreamWordSize.EightBit ? 1 : 2));
        return new RadarStreamEvent(
            sourceId,
            radarOrdinal: 0,
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks: 100,
            sourceRecord: 1,
            sourceMessage: 1,
            radialSequence: 1,
            elevationSlot: 0,
            azimuthBucket: (ushort)sourceId,
            rangeBand: 0,
            momentId: 0,
            gateStart: 0,
            gateCount: gateCount,
            wordSize: wordSize,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payloadOffset: payloadOffset,
            payloadLength: resolvedPayloadLength);
    }
}
