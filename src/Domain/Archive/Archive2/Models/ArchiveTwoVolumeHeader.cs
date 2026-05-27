namespace RadarPulse.Domain.Archive;

/// <summary>
/// Parsed 24-byte Archive II volume header metadata.
/// </summary>
public sealed record ArchiveTwoVolumeHeader(
    string ArchiveFilename,
    string Version,
    int ExtensionNumber,
    DateOnly VolumeDate,
    TimeSpan VolumeTime,
    DateTimeOffset VolumeTimestamp,
    string RadarId);

