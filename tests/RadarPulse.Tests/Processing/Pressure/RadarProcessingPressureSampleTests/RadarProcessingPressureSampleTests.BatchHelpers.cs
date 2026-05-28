using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingPressureSampleTests
{
    private static RadarEventBatch CreateEightBitBatch(
        SourceUniverseVersion sourceUniverseVersion,
        int[] sourceIds)
    {
        var events = new RadarStreamEvent[sourceIds.Length];
        var payload = new byte[sourceIds.Length];

        for (var i = 0; i < sourceIds.Length; i++)
        {
            events[i] = CreateEvent(
                sourceIds[i],
                messageTimestampUtcTicks: 100 + i,
                payloadOffset: i,
                gateCount: 1,
                wordSize: RadarStreamWordSize.EightBit);
            payload[i] = (byte)(i + 1);
        }

        return CreateBatch(sourceUniverseVersion, events, payload);
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
        int sourceId,
        long messageTimestampUtcTicks,
        byte[] payload)
    {
        builder.AddEvent(
            CreateIdentity(sourceId),
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks: messageTimestampUtcTicks,
            sourceRecord: 1,
            sourceMessage: 1,
            radialSequence: 1,
            gateStart: 0,
            gateCount: (ushort)payload.Length,
            wordSize: RadarStreamWordSize.EightBit,
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
