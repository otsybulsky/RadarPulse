using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Product;

internal static class RadarPulseProductSyntheticBatchFactory
{
    public static IReadOnlyList<RadarEventBatch> CreateBatches(
        RadarSourceUniverse universe,
        int batchCount,
        int eventsPerBatch)
    {
        var batches = new RadarEventBatch[batchCount];
        for (var batchIndex = 0; batchIndex < batchCount; batchIndex++)
        {
            batches[batchIndex] = CreateBatch(
                universe,
                eventsPerBatch,
                messageTimestampBase: 100 + (batchIndex * 1000));
        }

        return Array.AsReadOnly(batches);
    }

    private static RadarEventBatch CreateBatch(
        RadarSourceUniverse universe,
        int eventsPerBatch,
        long messageTimestampBase)
    {
        var builder = new RadarEventBatchBuilder(
            initialEventCapacity: eventsPerBatch,
            initialPayloadCapacity: eventsPerBatch);
        for (var i = 0; i < eventsPerBatch; i++)
        {
            var sourceId = i % universe.SourceCount;
            builder.AddEvent(
                new RadarStreamIdentity(
                    sourceId,
                    radarOrdinal: 0,
                    momentId: 0,
                    elevationSlot: 0,
                    azimuthBucket: (ushort)sourceId,
                    rangeBand: 0,
                    dictionaryVersion: DictionaryVersion.Initial,
                    sourceUniverseVersion: universe.Version),
                volumeTimestampUtcTicks: 90,
                messageTimestampUtcTicks: messageTimestampBase + i,
                sourceRecord: 1,
                sourceMessage: 1,
                radialSequence: i,
                gateStart: 0,
                gateCount: 1,
                wordSize: RadarStreamWordSize.EightBit,
                scale: 1.0f,
                offset: 0.0f,
                statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                payload: new byte[] { (byte)(i + 1) });
        }

        return builder.Build();
    }
}
