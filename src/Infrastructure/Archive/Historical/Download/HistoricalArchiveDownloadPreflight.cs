namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Disk-space preflight result for a selected historical archive manifest.
/// </summary>
public sealed record HistoricalArchiveDownloadPreflight(
    long RequiredDownloadBytes,
    long AvailableBytes);
