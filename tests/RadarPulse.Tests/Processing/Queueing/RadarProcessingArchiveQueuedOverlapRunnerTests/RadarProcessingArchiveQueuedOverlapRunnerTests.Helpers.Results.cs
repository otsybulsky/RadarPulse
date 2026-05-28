using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingArchiveQueuedOverlapRunnerTests
{
    private static RadarProcessingResult CreateProcessingResult()
    {
        var metrics = new RadarProcessingMetrics(
            ProcessedBatchCount: 1,
            ProcessedStreamEventCount: 1,
            ProcessedPayloadValueCount: 2,
            ActiveSourceCount: 1,
            RawValueChecksum: 3,
            ProcessingChecksum: 7);

        return new RadarProcessingResult(
            RadarProcessingExecutionMode.Sequential,
            partitionCount: 1,
            shardCount: 1,
            metrics,
            RadarProcessingValidationResult.Valid(metrics));
    }

    private static ArchiveRadarEventBatchPublishResult CreatePublishResult(
        long batchCount)
    {
        var normalizer = new RadarStreamIdentityNormalizer(
            ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse);
        return new ArchiveRadarEventBatchPublishResult(
            FilePath: "synthetic",
            Decompressor: "synthetic",
            DegreeOfParallelism: 1,
            FileSizeBytes: batchCount * 2,
            CompressedRecordCount: checked((int)batchCount),
            CompressedBytes: batchCount,
            DecompressedBytes: batchCount * 2,
            StreamSchemaVersion: StreamSchemaVersion.Current,
            DictionaryVersion: DictionaryVersion.Initial,
            SourceUniverseVersion: SourceUniverseVersion.Initial,
            BatchCount: batchCount,
            EventCount: batchCount,
            PayloadBytes: batchCount * 2,
            PayloadValueCount: batchCount * 2,
            RawValueChecksum: 0,
            DictionarySnapshot: normalizer.CreateDictionarySnapshot(DictionaryVersion.Initial));
    }

    private static void PublishLeased(
        IArchiveRadarEventBatchPublisher publisher,
        byte[] payload,
        CancellationToken cancellationToken,
        int sourceId = 0)
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: payload.Length);
        builder.AddEvent(
            new RadarStreamIdentity(
                sourceId,
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

        builder.ConsumeLeased(batch => publisher.Publish(batch, cancellationToken));
    }
}
