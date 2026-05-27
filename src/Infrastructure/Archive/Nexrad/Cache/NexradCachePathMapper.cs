using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class NexradCachePathMapper
{
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
