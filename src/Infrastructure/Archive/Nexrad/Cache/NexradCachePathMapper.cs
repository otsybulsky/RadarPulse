using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Maps NEXRAD archive manifest entries into the repository-local cache directory structure.
/// </summary>
public sealed class NexradCachePathMapper
{
    /// <summary>
    /// Maps an archive file entry to its deterministic local cache path.
    /// </summary>
    public string MapToLocalPath(string outputDirectory, HistoricalArchiveFile file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        return Path.Combine(
            outputDirectory,
            "level2",
            file.ArchiveDate.Year.ToString("0000"),
            file.ArchiveDate.Month.ToString("00"),
            file.ArchiveDate.Day.ToString("00"),
            file.RadarId,
            file.FileName);
    }
}
