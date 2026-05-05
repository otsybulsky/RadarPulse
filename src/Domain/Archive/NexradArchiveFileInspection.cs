namespace RadarPulse.Domain.Archive;

public sealed record NexradArchiveFileInspection(
    string FilePath,
    long SizeBytes,
    NexradArchiveFileKind FileKind,
    ArchiveTwoVolumeHeader? ArchiveTwoVolumeHeader,
    string? Diagnostic);


