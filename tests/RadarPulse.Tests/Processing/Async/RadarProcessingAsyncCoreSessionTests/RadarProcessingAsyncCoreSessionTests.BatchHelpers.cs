using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncCoreSessionTests
{
    private static RadarEventBatch CreateEmptyBatch(
        SourceUniverseVersion sourceUniverseVersion) =>
        CreateBatch(
            sourceUniverseVersion,
            Array.Empty<RadarStreamEvent>(),
            Array.Empty<byte>());

    private static RadarEventBatch CreateMixedBatch(
        SourceUniverseVersion sourceUniverseVersion) =>
        CreateMixedBatchBuilder(sourceUniverseVersion).Build();

    private static RadarEventBatchBuilder CreateMixedBatchBuilder(
        SourceUniverseVersion sourceUniverseVersion)
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 6, initialPayloadCapacity: 24);
        AddEvent(builder, sourceUniverseVersion, sourceId: 0, messageTimestampUtcTicks: 100, payload: new byte[] { 1, 2, 3, 4 });
        AddEvent(builder, sourceUniverseVersion, sourceId: 3, messageTimestampUtcTicks: 101, payload: new byte[] { 0, 5, 1, 0 },
            wordSize: RadarStreamWordSize.SixteenBit);
        AddEvent(builder, sourceUniverseVersion, sourceId: 1, messageTimestampUtcTicks: 102, payload: new byte[] { 5, 6, 7, 8 });
        AddEvent(builder, sourceUniverseVersion, sourceId: 5, messageTimestampUtcTicks: 103, payload: new byte[] { 2, 0, 0, 1 },
            wordSize: RadarStreamWordSize.SixteenBit);
        AddEvent(builder, sourceUniverseVersion, sourceId: 3, messageTimestampUtcTicks: 104, payload: new byte[] { 9, 10, 11, 12 });
        return builder;
    }

    private static RadarEventBatch CreateBatch(
        SourceUniverseVersion sourceUniverseVersion,
        RadarStreamEvent[] events,
        byte[] payload) =>
        new(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            events,
            payload);

    private static void AddEvent(
        RadarEventBatchBuilder builder,
        SourceUniverseVersion sourceUniverseVersion,
        int sourceId,
        long messageTimestampUtcTicks,
        byte[] payload,
        RadarStreamWordSize wordSize = RadarStreamWordSize.EightBit)
    {
        builder.AddEvent(
            CreateIdentity(sourceUniverseVersion, sourceId),
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

    private static RadarStreamIdentity CreateIdentity(
        SourceUniverseVersion sourceUniverseVersion,
        int sourceId) =>
        new(
            sourceId,
            radarOrdinal: 0,
            momentId: 0,
            elevationSlot: 0,
            azimuthBucket: (ushort)sourceId,
            rangeBand: 0,
            dictionaryVersion: DictionaryVersion.Initial,
            sourceUniverseVersion: sourceUniverseVersion);
}
