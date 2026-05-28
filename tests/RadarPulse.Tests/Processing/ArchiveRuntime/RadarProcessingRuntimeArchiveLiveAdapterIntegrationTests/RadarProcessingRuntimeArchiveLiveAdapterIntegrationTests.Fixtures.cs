using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests
{
    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarEventBatch CreateEightBitBatch(
        SourceUniverseVersion sourceUniverseVersion,
        int[] sourceIds,
        long messageTimestampBase)
    {
        var events = new RadarStreamEvent[sourceIds.Length];
        var payload = new byte[sourceIds.Length];

        for (var i = 0; i < sourceIds.Length; i++)
        {
            events[i] = new RadarStreamEvent(
                sourceIds[i],
                radarOrdinal: 0,
                volumeTimestampUtcTicks: 90,
                messageTimestampUtcTicks: messageTimestampBase + i,
                sourceRecord: 1,
                sourceMessage: 1,
                radialSequence: i,
                elevationSlot: 0,
                azimuthBucket: (ushort)sourceIds[i],
                rangeBand: 0,
                momentId: 0,
                gateStart: 0,
                gateCount: 1,
                wordSize: RadarStreamWordSize.EightBit,
                scale: 1.0f,
                offset: 0.0f,
                statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                payloadOffset: i,
                payloadLength: 1);
            payload[i] = (byte)(i + 1);
        }

        return new RadarEventBatch(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            events,
            payload);
    }

    private static RadarEventBatch CreateInvalidSourceBatch(
        SourceUniverseVersion sourceUniverseVersion) =>
        new(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            new[]
            {
                new RadarStreamEvent(
                    sourceId: 99,
                    radarOrdinal: 0,
                    volumeTimestampUtcTicks: 90,
                    messageTimestampUtcTicks: 100,
                    sourceRecord: 1,
                    sourceMessage: 1,
                    radialSequence: 0,
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
                    payloadLength: 1)
            },
            new byte[] { 1 });

    private static RadarEventBatch CreateEmptyBatch(
        SourceUniverseVersion sourceUniverseVersion) =>
        new(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            Array.Empty<RadarStreamEvent>(),
            Array.Empty<byte>());

    private static ArchiveRadarEventBatchPublishResult CreatePublishResult(
        RadarSourceUniverse universe,
        IReadOnlyCollection<RadarEventBatch> batches)
    {
        var normalizer = new RadarStreamIdentityNormalizer(universe);
        var eventCount = batches.Sum(static batch => batch.Events.Length);
        var payloadBytes = batches.Sum(static batch => batch.Payload.Length);
        return new ArchiveRadarEventBatchPublishResult(
            FilePath: "deterministic-live-adapter",
            Decompressor: "synthetic",
            DegreeOfParallelism: 1,
            FileSizeBytes: payloadBytes,
            CompressedRecordCount: batches.Count,
            CompressedBytes: payloadBytes,
            DecompressedBytes: payloadBytes,
            StreamSchemaVersion: StreamSchemaVersion.Current,
            DictionaryVersion: DictionaryVersion.Initial,
            SourceUniverseVersion: universe.Version,
            BatchCount: batches.Count,
            EventCount: eventCount,
            PayloadBytes: payloadBytes,
            PayloadValueCount: payloadBytes,
            RawValueChecksum: 0,
            DictionarySnapshot: normalizer.CreateDictionarySnapshot(DictionaryVersion.Initial));
    }
}
