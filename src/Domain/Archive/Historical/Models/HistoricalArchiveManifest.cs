namespace RadarPulse.Domain.Archive;

/// <summary>
/// Ordered set of archive files discovered for a historical archive date.
/// </summary>
public sealed record HistoricalArchiveManifest(
    DateOnly ArchiveDate,
    IReadOnlyList<HistoricalArchiveFile> Files)
{
    /// <summary>
    /// Computes aggregate file and byte totals grouped by radar id.
    /// </summary>
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
