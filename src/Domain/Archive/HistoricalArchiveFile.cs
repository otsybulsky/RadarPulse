namespace RadarPulse.Domain.Archive;

public sealed record HistoricalArchiveFile(
    string RadarId,
    DateOnly ArchiveDate,
    string ArchivePath,
    string FileName,
    long SizeBytes,
    DateTimeOffset LastModified,
    DateTimeOffset? VolumeTimestamp);
