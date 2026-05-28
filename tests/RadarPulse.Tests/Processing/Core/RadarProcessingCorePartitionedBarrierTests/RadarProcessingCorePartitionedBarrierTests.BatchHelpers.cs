using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingCorePartitionedBarrierTests
{
    private static RadarEventBatch CreateMixedBatch() =>
        CreateMixedBatchBuilder().Build();

    private static RadarEventBatchBuilder CreateMixedBatchBuilder()
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 6, initialPayloadCapacity: 24);
        AddEvent(builder, sourceId: 0, messageTimestampUtcTicks: 100, payload: new byte[] { 1, 2, 3, 4 });
        AddEvent(builder, sourceId: 3, messageTimestampUtcTicks: 101, payload: new byte[] { 0, 5, 1, 0 },
            wordSize: RadarStreamWordSize.SixteenBit);
        AddEvent(builder, sourceId: 1, messageTimestampUtcTicks: 102, payload: new byte[] { 5, 6, 7, 8 });
        AddEvent(builder, sourceId: 5, messageTimestampUtcTicks: 103, payload: new byte[] { 2, 0, 0, 1 },
            wordSize: RadarStreamWordSize.SixteenBit);
        AddEvent(builder, sourceId: 3, messageTimestampUtcTicks: 104, payload: new byte[] { 9, 10, 11, 12 });
        return builder;
    }

    private static RadarEventBatch CreateBatch(
        SourceUniverseVersion sourceUniverseVersion,
        RadarStreamEvent[] events,
        byte[] payload,
        StreamSchemaVersion? streamSchemaVersion = null) =>
        new(
            streamSchemaVersion ?? StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            events,
            payload);

    private static void AddEvent(
        RadarEventBatchBuilder builder,
        int sourceId,
        long messageTimestampUtcTicks,
        byte[] payload,
        RadarStreamWordSize wordSize = RadarStreamWordSize.EightBit)
    {
        builder.AddEvent(
            CreateIdentity(sourceId),
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks: messageTimestampUtcTicks,
            sourceRecord: 1,
            sourceMessage: 1,
            radialSequence: 1,
            gateStart: 0,
            gateCount: (ushort)(wordSize == RadarStreamWordSize.EightBit
                ? payload.Length
                : payload.Length / sizeof(ushort)),
            wordSize: wordSize,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: payload);
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
}
