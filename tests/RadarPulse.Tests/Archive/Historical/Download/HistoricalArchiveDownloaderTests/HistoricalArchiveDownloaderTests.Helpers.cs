using System.Text.Json;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class HistoricalArchiveDownloaderTests
{
    private static HistoricalArchiveFile CreateFile(long sizeBytes, string fileName) =>
        new(
            "KTLX",
            new DateOnly(2026, 5, 4),
            $"2026/05/04/KTLX/{fileName}",
            fileName,
            sizeBytes,
            DateTimeOffset.Parse("2026-05-04T12:00:00Z"),
            new DateTimeOffset(2026, 5, 4, 12, 0, 0, TimeSpan.Zero));

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radarpulse-download-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
