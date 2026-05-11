using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class NexradArchiveCacheInspector
{
    private readonly NexradArchiveFileInspector fileInspector;

    public NexradArchiveCacheInspector()
        : this(new NexradArchiveFileInspector())
    {
    }

    public NexradArchiveCacheInspector(NexradArchiveFileInspector fileInspector)
    {
        this.fileInspector = fileInspector ?? throw new ArgumentNullException(nameof(fileInspector));
    }

    public async Task<NexradArchiveCacheInspection> InspectAsync(
        string cachePath,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);
        if (maxFiles <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFiles), "Max files must be greater than zero.");
        }

        var directoryInfo = new DirectoryInfo(cachePath);
        if (!directoryInfo.Exists)
        {
            throw new DirectoryNotFoundException($"NEXRAD cache directory was not found: {cachePath}");
        }

        var normalizedRadarId = string.IsNullOrWhiteSpace(radarId)
            ? null
            : HistoricalArchiveRequest.NormalizeRadarId(radarId);
        var inspections = new List<NexradArchiveFileInspection>();

        foreach (var fileInfo in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).OrderBy(file => file.FullName))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (inspections.Count >= maxFiles)
            {
                break;
            }

            if (!MatchesRadar(fileInfo, normalizedRadarId) ||
                !MatchesDate(fileInfo, date))
            {
                continue;
            }

            inspections.Add(await InspectFileSafelyAsync(fileInfo, cancellationToken));
        }

        return new NexradArchiveCacheInspection(
            directoryInfo.FullName,
            date,
            normalizedRadarId,
            inspections.Count,
            inspections);
    }

    private async Task<NexradArchiveFileInspection> InspectFileSafelyAsync(
        FileInfo fileInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            return await fileInspector.InspectAsync(fileInfo.FullName, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new NexradArchiveFileInspection(
                fileInfo.FullName,
                fileInfo.Exists ? fileInfo.Length : 0,
                NexradArchiveFileKind.Unknown,
                null,
                Array.Empty<ArchiveTwoCompressedRecordSummary>(),
                ex.Message);
        }
    }

    private static bool MatchesRadar(FileInfo fileInfo, string? radarId)
    {
        if (radarId is null)
        {
            return true;
        }

        return fileInfo.Name.StartsWith(radarId, StringComparison.OrdinalIgnoreCase) ||
            fileInfo.DirectoryName?.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => string.Equals(segment, radarId, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static bool MatchesDate(FileInfo fileInfo, DateOnly? date)
    {
        if (date is null)
        {
            return true;
        }

        return TryReadDateFromFileName(fileInfo.Name, out var fileNameDate) && fileNameDate == date ||
            PathContainsDate(fileInfo.FullName, date.Value);
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
