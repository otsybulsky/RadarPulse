namespace RadarPulse.Domain.Archive;

/// <summary>
/// Manifest entry for one historical archive object and its local download identity.
/// </summary>
public sealed record HistoricalArchiveFile(
    string RadarId,
    DateOnly ArchiveDate,
    string ArchivePath,
    string FileName,
    long SizeBytes,
    DateTimeOffset LastModified,
    DateTimeOffset? VolumeTimestamp);
