namespace RadarPulse.Infrastructure.Archive;

public sealed record HistoricalArchiveDownloadPreflight(
    long RequiredDownloadBytes,
    long AvailableBytes);
