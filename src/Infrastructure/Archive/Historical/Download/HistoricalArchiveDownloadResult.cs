namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Download result for a historical archive manifest selection.
/// </summary>
public sealed record HistoricalArchiveDownloadResult(
    int DownloadedFileCount,
    int SkippedFileCount,
    long DownloadedBytes,
    long SkippedBytes);
