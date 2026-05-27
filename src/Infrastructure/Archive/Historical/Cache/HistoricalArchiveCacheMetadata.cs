namespace RadarPulse.Infrastructure.Archive;

public sealed record HistoricalArchiveCacheMetadata(
    string ArchivePath,
    long SizeBytes,
    DateTimeOffset LastModified,
    DateTimeOffset CachedAt);
