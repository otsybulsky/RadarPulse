using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveOrderedProcessingBenchmark
{
    private static RadarSourceUniverse CreateCacheSourceUniverse(
        DirectoryInfo directoryInfo,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        CancellationToken cancellationToken) =>
        RadarProcessingArchiveBenchmarkCacheSelection.CreateSourceUniverse(
            directoryInfo,
            date,
            radarId,
            maxFiles,
            cancellationToken);
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
            if (totals.ExaminedFiles >= maxFiles)
            {
                break;
            }

            if (!RadarProcessingArchiveBenchmarkCacheSelection.MatchesRadar(fileInfo, radarId) ||
                !RadarProcessingArchiveBenchmarkCacheSelection.MatchesDate(fileInfo, date))
            {
                continue;
            }

            totals.ExaminedFiles++;
            if (!ArchiveTwoFileReader.IsArchiveTwoBaseData(fileInfo))
            {
                totals.SkippedFiles++;
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

    private ArchiveRadarEventBatchPublishResult CreateEmptyCacheAggregatePublishResult(
        string cachePath,
        RadarSourceUniverse sourceUniverse,
        int degreeOfParallelism)
    {
        var normalizer = new RadarStreamIdentityNormalizer(sourceUniverse);
        return new ArchiveRadarEventBatchPublishResult(
            cachePath,
            decompressor.Name,
            degreeOfParallelism,
            FileSizeBytes: 0,
            CompressedRecordCount: 0,
            CompressedBytes: 0,
            DecompressedBytes: 0,
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverse.Version,
            BatchCount: 0,
            EventCount: 0,
            PayloadBytes: 0,
            PayloadValueCount: 0,
            RawValueChecksum: 0,
            normalizer.CreateDictionarySnapshot(DictionaryVersion.Initial));
    }

}
