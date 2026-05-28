using System.Diagnostics;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Processing;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingHandlerDeltaPerformanceGateTests
{
    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarEventBatch CreateBatch(
        SourceUniverseVersion sourceUniverseVersion,
        int sourceCount,
        int eventCount,
        int payloadBytesPerEvent,
        long messageTimestampBase)
    {
        var builder = new RadarEventBatchBuilder(
            initialEventCapacity: eventCount,
            initialPayloadCapacity: checked(eventCount * payloadBytesPerEvent));
        for (var i = 0; i < eventCount; i++)
        {
            var sourceId = i % sourceCount;
            var payload = Enumerable.Range(0, payloadBytesPerEvent)
                .Select(offset => (byte)((i + offset) & 0xff))
                .ToArray();
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
                messageTimestampUtcTicks: messageTimestampBase + i,
                sourceRecord: 1,
                sourceMessage: 1,
                radialSequence: i,
                gateStart: 0,
                gateCount: checked((ushort)payloadBytesPerEvent),
                wordSize: RadarStreamWordSize.EightBit,
                scale: 1.0f + (i % 3),
                offset: 0.0f,
                statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                payload);
        }

        return builder.Build();
    }
}
