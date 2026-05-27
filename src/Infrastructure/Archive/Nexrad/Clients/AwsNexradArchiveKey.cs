using System.Globalization;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Helpers for AWS public NEXRAD Level II S3 object keys.
/// </summary>
public static class AwsNexradArchiveKey
{
    /// <summary>
    /// Public S3 bucket that hosts NEXRAD Level II archive data.
    /// </summary>
    public const string BucketName = "unidata-nexrad-level2";

    /// <summary>
    /// Builds the S3 prefix for all radar files on an archive date.
    /// </summary>
    public static string DatePrefix(DateOnly date) =>
        string.Create(CultureInfo.InvariantCulture, $"{date:yyyy/MM/dd}/");

    /// <summary>
    /// Builds the S3 prefix for one radar on an archive date.
    /// </summary>
    public static string RadarPrefix(DateOnly date, string radarId) =>
        $"{DatePrefix(date)}{HistoricalArchiveRequest.NormalizeRadarId(radarId)}/";

    /// <summary>
    /// Attempts to parse an S3 object key and metadata into a historical archive file entry.
    /// </summary>
    public static bool TryParse(
        string s3Key,
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

        var radarId = HistoricalArchiveRequest.NormalizeRadarId(parts[3]);
        var fileName = parts[^1];
        file = new HistoricalArchiveFile(
            radarId,
            archiveDate,
            s3Key,
            fileName,
            sizeBytes,
            lastModified,
            TryParseVolumeTimestamp(fileName, archiveDate));
        return true;
    }

    /// <summary>
    /// Attempts to parse the volume timestamp embedded in a NEXRAD archive file name.
    /// </summary>
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
