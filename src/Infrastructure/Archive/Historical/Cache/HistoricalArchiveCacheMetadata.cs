namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Sidecar metadata written next to downloaded historical archive files.
/// </summary>
public sealed record HistoricalArchiveCacheMetadata(
    string ArchivePath,
    long SizeBytes,
    DateTimeOffset LastModified,
    DateTimeOffset CachedAt);
