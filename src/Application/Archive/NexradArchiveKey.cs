using System.Globalization;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Application.Archive;

public static class NexradArchiveKey
{
    public const string BucketName = "unidata-nexrad-level2";

    public static string DatePrefix(DateOnly date) =>
        string.Create(CultureInfo.InvariantCulture, $"{date:yyyy/MM/dd}/");

    public static string RadarPrefix(DateOnly date, string radarId) =>
        $"{DatePrefix(date)}{NormalizeRadarId(radarId)}/";

    public static string NormalizeRadarId(string radarId)
    {
        var normalized = radarId.Trim().ToUpperInvariant();
        if (normalized.Length != 4 || !normalized.All(char.IsLetterOrDigit))
        {
            throw new ArgumentException("Radar id must be a 4-character alphanumeric identifier.", nameof(radarId));
        }

        return normalized;
    }

    public static bool TryParse(
        string s3Key,
        string bucket,
        long sizeBytes,
        DateTimeOffset lastModified,
        out HistoricalArchiveFile? file)
    {
        file = null;
        var parts = s3Key.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5 ||
            !int.TryParse(parts[0], out var year) ||
            !int.TryParse(parts[1], out var month) ||
            !int.TryParse(parts[2], out var day))
        {
            return false;
        }

        DateOnly archiveDate;
        try
        {
            archiveDate = new DateOnly(year, month, day);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        var radarId = parts[3].ToUpperInvariant();
        var fileName = parts[^1];
        file = new HistoricalArchiveFile(
            radarId,
            archiveDate,
            bucket,
            s3Key,
            fileName,
            sizeBytes,
            lastModified,
            TryParseVolumeTimestamp(fileName, archiveDate));
        return true;
    }

    public static DateTimeOffset? TryParseVolumeTimestamp(string fileName, DateOnly archiveDate)
    {
        const int timestampLength = 15;

        for (var i = 0; i <= fileName.Length - timestampLength; i++)
        {
            if (fileName[i + 8] != '_')
            {
                continue;
            }

            var datePart = fileName.Substring(i, 8);
            var timePart = fileName.Substring(i + 9, 6);
            if (!DateOnly.TryParseExact(datePart, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
                !TimeOnly.TryParseExact(timePart, "HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            {
                continue;
            }

            if (date != archiveDate)
            {
                return null;
            }

            return new DateTimeOffset(date.ToDateTime(time), TimeSpan.Zero);
        }

        return null;
    }
}
