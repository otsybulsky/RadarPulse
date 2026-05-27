namespace RadarPulse.Domain.Archive;

/// <summary>
/// Aggregate manifest summary across selected radar archive files.
/// </summary>
public sealed record HistoricalArchiveSummary(
    int RadarCount,
    int FileCount,
    long TotalBytes,
    IReadOnlyList<RadarArchiveSummary> Radars);

/// <summary>
/// Aggregate manifest summary for a single radar id.
/// </summary>
public sealed record RadarArchiveSummary(
    string RadarId,
    int FileCount,
    long TotalBytes);
