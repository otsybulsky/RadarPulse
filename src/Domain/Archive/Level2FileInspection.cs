namespace RadarPulse.Domain.Archive;

public sealed record Level2FileInspection(
    string FilePath,
    long SizeBytes,
    Level2FileClass FileClass,
    ArchiveIiVolumeHeader? ArchiveIiVolumeHeader,
    string? Diagnostic);
