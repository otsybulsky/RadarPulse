using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRuntimeArchiveBaselineTests
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
        int[] sourceIds)
    {
        var events = new RadarStreamEvent[sourceIds.Length];
        var payload = new byte[sourceIds.Length];

        for (var i = 0; i < sourceIds.Length; i++)
        {
            events[i] = new RadarStreamEvent(
                sourceIds[i],
                radarOrdinal: 0,
                volumeTimestampUtcTicks: 90,
                messageTimestampUtcTicks: 100 + i,
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

    private static ArchiveRadarEventBatchPublishResult CreatePublishResult(
        RadarSourceUniverse universe,
        long batchCount,
        long eventCount,
        long payloadBytes)
    {
        var normalizer = new RadarStreamIdentityNormalizer(universe);
        return new ArchiveRadarEventBatchPublishResult(
            FilePath: "synthetic",
            Decompressor: "synthetic",
            DegreeOfParallelism: 1,
            FileSizeBytes: payloadBytes,
            CompressedRecordCount: checked((int)batchCount),
            CompressedBytes: payloadBytes,
            DecompressedBytes: payloadBytes,
            StreamSchemaVersion: StreamSchemaVersion.Current,
            DictionaryVersion: DictionaryVersion.Initial,
            SourceUniverseVersion: universe.Version,
            BatchCount: batchCount,
            EventCount: eventCount,
            PayloadBytes: payloadBytes,
            PayloadValueCount: payloadBytes,
            RawValueChecksum: 0,
            DictionarySnapshot: normalizer.CreateDictionarySnapshot(DictionaryVersion.Initial));
    }
}
