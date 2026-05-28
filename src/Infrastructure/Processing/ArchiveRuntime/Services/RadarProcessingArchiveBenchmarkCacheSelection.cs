using RadarPulse.Application.Archive;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

internal static class RadarProcessingArchiveBenchmarkCacheSelection
{
    private const int MaxAutoSizedCacheRadarOrdinalCount = 256;

    public static RadarSourceUniverse CreateSourceUniverse(
        DirectoryInfo directoryInfo,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        CancellationToken cancellationToken)
    {
        if (radarId is not null)
        {
            return ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse;
        }

        var radarOrdinalCount = CountSelectedCacheRadarOrdinals(
            directoryInfo,
            date,
            radarId,
            maxFiles,
            cancellationToken);
        return CreateArchiveSourceUniverse(radarOrdinalCount);
    }

    public static bool MatchesRadar(FileInfo fileInfo, string? radarId)
    {
        if (radarId is null)
        {
            return true;
        }

        return fileInfo.Name.StartsWith(radarId, StringComparison.OrdinalIgnoreCase) ||
            fileInfo.DirectoryName?.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => string.Equals(segment, radarId, StringComparison.OrdinalIgnoreCase)) == true;
    }

    public static bool MatchesDate(FileInfo fileInfo, DateOnly? date)
    {
        if (date is null)
        {
            return true;
        }

        return TryReadDateFromFileName(fileInfo.Name, out var fileNameDate) && fileNameDate == date ||
            PathContainsDate(fileInfo.FullName, date.Value);
    }

    private static int CountSelectedCacheRadarOrdinals(
        DirectoryInfo directoryInfo,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        CancellationToken cancellationToken)
    {
        var baseDataFiles = SelectBaseDataFiles(directoryInfo, date, radarId, maxFiles, cancellationToken);
        if (baseDataFiles.Count == 0)
        {
            return ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse.RadarOrdinalCount;
        }

        var radarIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fileInfo in baseDataFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var header = ArchiveTwoFileReader.ReadVolumeHeader(fileInfo);
            radarIds.Add(header.RadarId);
            if (radarIds.Count > MaxAutoSizedCacheRadarOrdinalCount)
            {
                throw new InvalidOperationException(
                    $"Cache benchmark auto-sized source universe supports at most {MaxAutoSizedCacheRadarOrdinalCount} distinct radar ids. " +
                    "Pass a radar id filter or reduce max files.");
            }
        }

        return Math.Max(1, radarIds.Count);
    }

    private static IReadOnlyList<FileInfo> SelectBaseDataFiles(
        DirectoryInfo directoryInfo,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        CancellationToken cancellationToken)
    {
        var examinedFileCount = 0;
        var baseDataFiles = new List<FileInfo>();
        foreach (var fileInfo in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).OrderBy(file => file.FullName))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (examinedFileCount >= maxFiles)
            {
                break;
            }

            if (!MatchesRadar(fileInfo, radarId) ||
                !MatchesDate(fileInfo, date))
            {
                continue;
            }

            examinedFileCount++;
            if (ArchiveTwoFileReader.IsArchiveTwoBaseData(fileInfo))
            {
                baseDataFiles.Add(fileInfo);
            }
        }

        return baseDataFiles;
    }

    private static RadarSourceUniverse CreateArchiveSourceUniverse(int radarOrdinalCount)
    {
        var defaultUniverse = ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse;
        if (radarOrdinalCount == defaultUniverse.RadarOrdinalCount)
        {
            return defaultUniverse;
        }

        return new RadarSourceUniverse(
            defaultUniverse.Version,
            radarOrdinalCount,
            defaultUniverse.ElevationSlotCount,
            defaultUniverse.AzimuthBucketCount,
            defaultUniverse.RangeBandCount);
    }

    private static bool TryReadDateFromFileName(string fileName, out DateOnly date)
    {
        date = default;
        if (fileName.Length < 12)
        {
            return false;
        }

        var dateText = fileName.AsSpan(4, 8);
        if (!int.TryParse(dateText[..4], out var year) ||
            !int.TryParse(dateText.Slice(4, 2), out var month) ||
            !int.TryParse(dateText.Slice(6, 2), out var day))
        {
            return false;
        }

        try
        {
            date = new DateOnly(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool PathContainsDate(string path, DateOnly date)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var i = 0; i <= segments.Length - 3; i++)
        {
            if (string.Equals(segments[i], date.Year.ToString("0000"), StringComparison.Ordinal) &&
                string.Equals(segments[i + 1], date.Month.ToString("00"), StringComparison.Ordinal) &&
                string.Equals(segments[i + 2], date.Day.ToString("00"), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
