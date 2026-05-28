using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingPersistentDurableEnvelopeStoreTests
{
    private static RadarProcessingPersistentDurableEnvelopeRecord CreateRecord(
        string batchId)
    {
        var batch = CreateBatch();
        var queuedBatch = new RadarProcessingQueuedBatch(
            RadarProcessingQueuedBatchSequence.Initial,
            batch,
            enqueuedTimestamp: 1,
            payloadValueCount: 2,
            rawValueChecksum: 3);
        var snapshot = new RadarProcessingDurableEnvelopeSnapshot(
            new RadarProcessingDurableBatchId(batchId),
            queuedBatch.Sequence,
            attempt: 0,
            RadarProcessingDurableEnvelopeState.Pending,
            streamEventCount: queuedBatch.StreamEventCount,
            payloadBytes: queuedBatch.PayloadBytes,
            payloadValueCount: queuedBatch.PayloadValueCount,
            rawValueChecksum: queuedBatch.RawValueChecksum,
            acceptedTimestamp: 1);

        return RadarProcessingPersistentDurableEnvelopeRecord.From(snapshot, queuedBatch);
    }

    private static RadarEventBatch CreateBatch()
    {
        var events = new[]
        {
            new RadarStreamEvent(
                sourceId: 0,
                radarOrdinal: 0,
                volumeTimestampUtcTicks: 90,
                messageTimestampUtcTicks: 100,
                sourceRecord: 1,
                sourceMessage: 1,
                radialSequence: 1,
                elevationSlot: 0,
                azimuthBucket: 0,
                rangeBand: 0,
                momentId: 0,
                gateStart: 0,
                gateCount: 1,
                wordSize: RadarStreamWordSize.EightBit,
                scale: 1.0f,
                offset: 0.0f,
                statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                payloadOffset: 0,
                payloadLength: 1),
            new RadarStreamEvent(
                sourceId: 1,
                radarOrdinal: 0,
                volumeTimestampUtcTicks: 90,
                messageTimestampUtcTicks: 200,
                sourceRecord: 1,
                sourceMessage: 1,
                radialSequence: 2,
                elevationSlot: 0,
                azimuthBucket: 1,
                rangeBand: 0,
                momentId: 0,
                gateStart: 0,
                gateCount: 1,
                wordSize: RadarStreamWordSize.EightBit,
                scale: 1.0f,
                offset: 0.0f,
                statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                payloadOffset: 1,
                payloadLength: 1)
        };

        return new RadarEventBatch(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            SourceUniverseVersion.Initial,
            events,
            new byte[] { 1, 2 });
    }

    private static long[] CreateMessageTimestampArray(
        RadarEventBatch batch)
    {
        var timestamps = new long[batch.Events.Length];
        var events = batch.Events.Span;
        for (var i = 0; i < events.Length; i++)
        {
            timestamps[i] = events[i].MessageTimestampUtcTicks;
        }

        return timestamps;
    }
}
