namespace RadarPulse.Domain.Archive;

public sealed record HistoricalArchiveSummary(
    int RadarCount,
    int FileCount,
    long TotalBytes,
    IReadOnlyList<RadarArchiveSummary> Radars);

public sealed record RadarArchiveSummary(
    string RadarId,
    int FileCount,
    long TotalBytes);
