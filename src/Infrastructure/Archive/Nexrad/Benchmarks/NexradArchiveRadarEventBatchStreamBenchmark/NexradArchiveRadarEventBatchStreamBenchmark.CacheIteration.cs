using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveRadarEventBatchStreamBenchmark
{
    private static bool HasSameTotals(
        ArchiveRadarEventBatchPublishResult expected,
        RadarStreamDictionarySnapshotMetrics expectedDictionaryMetrics,
        ArchiveRadarEventBatchPublishResult actual,
        RadarStreamDictionarySnapshotMetrics actualDictionaryMetrics) =>
        expected.FileSizeBytes == actual.FileSizeBytes &&
        expected.CompressedRecordCount == actual.CompressedRecordCount &&
        expected.CompressedBytes == actual.CompressedBytes &&
        expected.DecompressedBytes == actual.DecompressedBytes &&
        expected.StreamSchemaVersion == actual.StreamSchemaVersion &&
        expected.DictionaryVersion == actual.DictionaryVersion &&
        expected.SourceUniverseVersion == actual.SourceUniverseVersion &&
        expected.BatchCount == actual.BatchCount &&
        expected.EventCount == actual.EventCount &&
        expected.PayloadBytes == actual.PayloadBytes &&
        expected.PayloadValueCount == actual.PayloadValueCount &&
        expected.RawValueChecksum == actual.RawValueChecksum &&
        expectedDictionaryMetrics == actualDictionaryMetrics;

    private static CacheIterationTotals PublishCacheIteration(
        DirectoryInfo directoryInfo,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        NexradArchiveRadarEventBatchPublishSession session,
        CancellationToken cancellationToken)
    {
        var totals = new CacheIterationTotals(
            StreamSchemaVersion.Current,
            SourceUniverseVersion.Initial);

        foreach (var fileInfo in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).OrderBy(file => file.FullName))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (totals.ExaminedFiles >= maxFiles)
            {
                break;
            }

            if (!MatchesRadar(fileInfo, radarId) ||
                !MatchesDate(fileInfo, date))
            {
                continue;
            }

            totals.ExaminedFiles++;
            if (!ArchiveTwoFileReader.IsArchiveTwoBaseData(fileInfo))
            {
                totals.SkippedFiles++;
                continue;
            }

            var result = session.PublishFile(fileInfo.FullName, cancellationToken);
            totals.Add(result);
        }

        return totals;
    }
}
