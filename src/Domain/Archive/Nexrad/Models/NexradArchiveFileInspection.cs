namespace RadarPulse.Domain.Archive;

/// <summary>
/// Inspection result for one NEXRAD cache file, including Archive II metadata when parseable.
/// </summary>
public sealed record NexradArchiveFileInspection(
    string FilePath,
    long SizeBytes,
    NexradArchiveFileKind FileKind,
    ArchiveTwoVolumeHeader? ArchiveTwoVolumeHeader,
    IReadOnlyList<ArchiveTwoCompressedRecordSummary> CompressedRecords,
    string? Diagnostic,
    ArchiveTwoMessageSummary? MessageSummary = null);


