namespace RadarPulse.Infrastructure.Archive;

public sealed record HistoricalArchiveDownloadResult(
    int DownloadedFileCount,
    int SkippedFileCount,
    long DownloadedBytes,
    long SkippedBytes);
