using System.Buffers;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Archive;

public sealed partial class ArchiveOwnedRadarEventBatchQueueingPublisherTests
{
    private static void PublishLeased(
        ArchiveOwnedRadarEventBatchQueueingPublisher publisher,
        byte[] payload,
        CancellationToken cancellationToken = default)
    {
        var builder = CreateBuilder(payload);
        builder.ConsumeLeased(batch => publisher.Publish(batch, cancellationToken));
    }

    private static RadarEventBatchBuilder CreateBuilder(byte[] payload)
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: payload.Length);
        AddEvent(builder, payload);
        return builder;
    }

    private static void AddEvent(
        RadarEventBatchBuilder builder,
        byte[] payload)
    {
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
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks: 100,
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
}
