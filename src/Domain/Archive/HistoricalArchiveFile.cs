namespace RadarPulse.Domain.Archive;

public sealed record HistoricalArchiveFile(
    string RadarId,
    DateOnly ArchiveDate,
    string Bucket,
    string S3Key,
    string FileName,
    long SizeBytes,
    DateTimeOffset LastModified,
    DateTimeOffset? VolumeTimestamp);
