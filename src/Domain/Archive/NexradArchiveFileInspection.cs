namespace RadarPulse.Domain.Archive;

public sealed record NexradArchiveFileInspection(
    string FilePath,
    long SizeBytes,
    NexradArchiveFileKind FileKind,
    ArchiveTwoVolumeHeader? ArchiveTwoVolumeHeader,
    IReadOnlyList<ArchiveTwoCompressedRecordSummary> CompressedRecords,
    string? Diagnostic,
    ArchiveTwoMessageSummary? MessageSummary = null);


