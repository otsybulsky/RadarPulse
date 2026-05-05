namespace RadarPulse.Domain.Archive;

public sealed record ArchiveIiVolumeHeader(
    string ArchiveFilename,
    string Version,
    int ExtensionNumber,
    DateOnly VolumeDate,
    TimeSpan VolumeTime,
    DateTimeOffset VolumeTimestamp,
    string RadarId);
