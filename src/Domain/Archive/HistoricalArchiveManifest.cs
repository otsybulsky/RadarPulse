namespace RadarPulse.Domain.Archive;

public sealed record HistoricalArchiveManifest(
    DateOnly ArchiveDate,
    string Bucket,
    IReadOnlyList<HistoricalArchiveFile> Files)
{
    public HistoricalArchiveSummary Summarize()
    {
        var radarSummaries = Files
            .GroupBy(file => file.RadarId, StringComparer.OrdinalIgnoreCase)
            .Select(group => new RadarArchiveSummary(
                group.Key,
                group.Count(),
                group.Sum(file => file.SizeBytes)))
            .OrderBy(summary => summary.RadarId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new HistoricalArchiveSummary(
            radarSummaries.Length,
            Files.Count,
            Files.Sum(file => file.SizeBytes),
            radarSummaries);
    }
}
