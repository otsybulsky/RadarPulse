using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveRadarEventBatchStreamBenchmark
{
    private struct CacheIterationTotals
    {
        public CacheIterationTotals(
            StreamSchemaVersion streamSchemaVersion,
            SourceUniverseVersion sourceUniverseVersion)
            : this()
        {
            StreamSchemaVersion = streamSchemaVersion;
            SourceUniverseVersion = sourceUniverseVersion;
        }

        public StreamSchemaVersion StreamSchemaVersion;
        public SourceUniverseVersion SourceUniverseVersion;
        public int ExaminedFiles;
        public int SkippedFiles;
        public int PublishedFiles;
        public long FileSizeBytes;
        public int CompressedRecordCount;
        public long CompressedBytes;
        public long DecompressedBytes;
        public long BatchCount;
        public long EventCount;
        public long PayloadBytes;
        public long PayloadValueCount;
        public long RawValueChecksum;

        public void Add(ArchiveRadarEventBatchPublishResult result)
        {
            if (PublishedFiles == 0)
            {
                StreamSchemaVersion = result.StreamSchemaVersion;
                SourceUniverseVersion = result.SourceUniverseVersion;
            }
            else if (StreamSchemaVersion != result.StreamSchemaVersion ||
                SourceUniverseVersion != result.SourceUniverseVersion)
            {
                throw new InvalidDataException("Radar event batch stream cache benchmark produced mixed stream schema or source-universe versions.");
            }

            PublishedFiles++;
            FileSizeBytes += result.FileSizeBytes;
            CompressedRecordCount += result.CompressedRecordCount;
            CompressedBytes += result.CompressedBytes;
            DecompressedBytes += result.DecompressedBytes;
            BatchCount += result.BatchCount;
            EventCount += result.EventCount;
            PayloadBytes += result.PayloadBytes;
            PayloadValueCount += result.PayloadValueCount;
            RawValueChecksum += result.RawValueChecksum;
        }

        public readonly bool HasSameTotals(CacheIterationTotals other) =>
            StreamSchemaVersion == other.StreamSchemaVersion &&
            SourceUniverseVersion == other.SourceUniverseVersion &&
            ExaminedFiles == other.ExaminedFiles &&
            SkippedFiles == other.SkippedFiles &&
            PublishedFiles == other.PublishedFiles &&
            FileSizeBytes == other.FileSizeBytes &&
            CompressedRecordCount == other.CompressedRecordCount &&
            CompressedBytes == other.CompressedBytes &&
            DecompressedBytes == other.DecompressedBytes &&
            BatchCount == other.BatchCount &&
            EventCount == other.EventCount &&
            PayloadBytes == other.PayloadBytes &&
            PayloadValueCount == other.PayloadValueCount &&
            RawValueChecksum == other.RawValueChecksum;
    }
}
