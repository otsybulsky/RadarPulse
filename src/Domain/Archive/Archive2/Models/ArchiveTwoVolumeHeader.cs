namespace RadarPulse.Domain.Archive;

public sealed record ArchiveTwoVolumeHeader(
    string ArchiveFilename,
    string Version,
    int ExtensionNumber,
    DateOnly VolumeDate,
    TimeSpan VolumeTime,
    DateTimeOffset VolumeTimestamp,
    string RadarId);

