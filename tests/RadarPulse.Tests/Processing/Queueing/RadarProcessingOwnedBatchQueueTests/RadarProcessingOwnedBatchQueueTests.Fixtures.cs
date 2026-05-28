using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingOwnedBatchQueueTests
{
    private static RadarEventBatch CreateOwnedBatch(byte firstPayloadValue) =>
        CreateOwnedBatch([firstPayloadValue, (byte)(firstPayloadValue + 1)]);

    private static RadarEventBatch CreateOwnedBatch(byte[] payload) =>
        CreateBatchBuilder(payload).Build();

    private static RadarEventBatchBuilder CreateBatchBuilder(byte firstPayloadValue)
        => CreateBatchBuilder([firstPayloadValue, (byte)(firstPayloadValue + 1)]);

    private static RadarEventBatchBuilder CreateBatchBuilder(byte[] payload)
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: 2);
        builder.AddEvent(
            new RadarStreamIdentity(
                sourceId: 0,
                radarOrdinal: 0,
                momentId: 0,
                elevationSlot: 0,
                azimuthBucket: 0,
                rangeBand: 0,
                dictionaryVersion: DictionaryVersion.Initial,
                sourceUniverseVersion: SourceUniverseVersion.Initial),
            volumeTimestampUtcTicks: 1,
            messageTimestampUtcTicks: 2,
            sourceRecord: 0,
            sourceMessage: 0,
            radialSequence: 0,
            gateStart: 0,
            gateCount: (ushort)payload.Length,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: payload);

        return builder;
    }
}
