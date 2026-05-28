using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveRebalanceBenchmark
{
    private struct CacheIterationTotals
    {
        public static CacheIterationTotals Empty => new();

        public long ExaminedFileCount;
        public long SkippedFileCount;
        public long PublishedFileCount;
        public long FileSizeBytes;
        public long CompressedRecordCount;
        public long CompressedBytes;
        public long DecompressedBytes;
        public long BatchCount;
        public long EventCount;
        public long PayloadBytes;
        public long PayloadValueCount;
        public long RawValueChecksum;

        public void Add(RadarPulse.Domain.Archive.ArchiveRadarEventBatchPublishResult result)
        {
            PublishedFileCount = checked(PublishedFileCount + 1);
            FileSizeBytes = checked(FileSizeBytes + result.FileSizeBytes);
            CompressedRecordCount = checked(CompressedRecordCount + result.CompressedRecordCount);
            CompressedBytes = checked(CompressedBytes + result.CompressedBytes);
            DecompressedBytes = checked(DecompressedBytes + result.DecompressedBytes);
            BatchCount = checked(BatchCount + result.BatchCount);
            EventCount = checked(EventCount + result.EventCount);
            PayloadBytes = checked(PayloadBytes + result.PayloadBytes);
            PayloadValueCount = checked(PayloadValueCount + result.PayloadValueCount);
            RawValueChecksum = checked(RawValueChecksum + result.RawValueChecksum);
        }
    }

    private readonly record struct QueuedArchivePublishResult(
        ArchiveRadarEventBatchPublishResult PublishResult,
        RadarProcessingProviderQueueTelemetrySummary QueueTelemetry,
        RadarProcessingRetainedPayloadTelemetrySummary RetentionTelemetry,
        RadarProcessingArchiveOverlapTelemetrySummary OverlapTelemetry);

    private readonly record struct QueuedArchiveCachePublishResult(
        CacheIterationTotals Totals,
        RadarProcessingProviderQueueTelemetrySummary QueueTelemetry,
        RadarProcessingRetainedPayloadTelemetrySummary RetentionTelemetry,
        RadarProcessingArchiveOverlapTelemetrySummary OverlapTelemetry);

    private sealed record DefaultRetainedPayloadPrewarm(
        RadarProcessingRetainedPayloadFactory Factory,
        RadarProcessingRetainedPayloadPrewarmResult Result);

    private sealed class CacheArchiveFileSelection
    {
        public CacheArchiveFileSelection(
            CacheIterationTotals totals,
            IReadOnlyList<FileInfo> baseDataFiles)
        {
            Totals = totals;
            BaseDataFiles = baseDataFiles ?? throw new ArgumentNullException(nameof(baseDataFiles));
        }

        public CacheIterationTotals Totals { get; }

        public IReadOnlyList<FileInfo> BaseDataFiles { get; }
    }
}
