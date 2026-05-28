using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveRebalanceBenchmark
{
    private static CacheArchiveFileSelection SelectCacheArchiveFiles(
        DirectoryInfo directoryInfo,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        CancellationToken cancellationToken)
    {
        var totals = CacheIterationTotals.Empty;
        var baseDataFiles = new List<FileInfo>();
        foreach (var fileInfo in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).OrderBy(file => file.FullName))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (totals.ExaminedFileCount >= maxFiles)
            {
                break;
            }

            if (!RadarProcessingArchiveBenchmarkCacheSelection.MatchesRadar(fileInfo, radarId) ||
                !RadarProcessingArchiveBenchmarkCacheSelection.MatchesDate(fileInfo, date))
            {
                continue;
            }

            totals.ExaminedFileCount++;
            if (!ArchiveTwoFileReader.IsArchiveTwoBaseData(fileInfo))
            {
                totals.SkippedFileCount++;
                continue;
            }

            baseDataFiles.Add(fileInfo);
        }

        return new CacheArchiveFileSelection(totals, baseDataFiles);
    }

    private static ArchiveRadarEventBatchPublishResult CreateCacheAggregatePublishResult(
        string cachePath,
        CacheIterationTotals totals,
        ArchiveRadarEventBatchPublishResult lastPublishResult) =>
        new(
            cachePath,
            lastPublishResult.Decompressor,
            lastPublishResult.DegreeOfParallelism,
            totals.FileSizeBytes,
            checked((int)totals.CompressedRecordCount),
            totals.CompressedBytes,
            totals.DecompressedBytes,
            lastPublishResult.StreamSchemaVersion,
            lastPublishResult.DictionaryVersion,
            lastPublishResult.SourceUniverseVersion,
            totals.BatchCount,
            totals.EventCount,
            totals.PayloadBytes,
            totals.PayloadValueCount,
            totals.RawValueChecksum,
            lastPublishResult.DictionarySnapshot);

}
