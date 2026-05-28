using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedProcessingSessionTests
{
    private static void AddEvent(
        RadarEventBatchBuilder builder,
        SourceUniverseVersion sourceUniverseVersion,
        int sourceId,
        long messageTimestampUtcTicks,
        byte[] payload)
    {
        builder.AddEvent(
            new RadarStreamIdentity(
                sourceId,
                radarOrdinal: 0,
                momentId: 0,
                elevationSlot: 0,
                azimuthBucket: (ushort)sourceId,
                rangeBand: 0,
                dictionaryVersion: DictionaryVersion.Initial,
                sourceUniverseVersion: sourceUniverseVersion),
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks: messageTimestampUtcTicks,
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
    }

    private static void PublishLeased(
        ArchiveOwnedRadarEventBatchQueueingPublisher publisher,
        SourceUniverseVersion sourceUniverseVersion,
        byte[] payload)
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: payload.Length);
        AddEvent(
            builder,
            sourceUniverseVersion,
            sourceId: 0,
            messageTimestampUtcTicks: 100,
            payload);
        builder.ConsumeLeased(batch => publisher.Publish(batch, CancellationToken.None));
    }
}
