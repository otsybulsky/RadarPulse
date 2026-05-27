namespace RadarPulse.Domain.Archive;

/// <summary>
/// Aggregate inspection result for a NEXRAD cache directory.
/// </summary>
public sealed record NexradArchiveCacheInspection(
    string CachePath,
    DateOnly? Date,
    string? RadarId,
    int ExaminedFileCount,
    IReadOnlyList<NexradArchiveFileInspection> Files)
{
    /// <summary>
    /// Gets the number of inspected files recognized as Archive II base-data volumes.
    /// </summary>
    public int ArchiveTwoBaseDataFileCount =>
        Files.Count(file => file.FileKind == NexradArchiveFileKind.ArchiveTwoBaseData);

    /// <summary>
    /// Gets the number of inspected files recognized as MDM or compressed stream objects.
    /// </summary>
    public int MdmOrCompressedStreamFileCount =>
        Files.Count(file => file.FileKind == NexradArchiveFileKind.MdmOrCompressedStream);

    /// <summary>
    /// Gets the number of inspected files with unrecognized signatures.
    /// </summary>
    public int UnknownFileCount =>
        Files.Count(file => file.FileKind == NexradArchiveFileKind.Unknown);

    /// <summary>
    /// Gets the number of files or compressed records that produced an inspection diagnostic.
    /// </summary>
    public int DiagnosticFileCount =>
        Files.Count(file =>
            !string.IsNullOrWhiteSpace(file.Diagnostic) ||
            file.CompressedRecords.Any(record => !string.IsNullOrWhiteSpace(record.DecompressionDiagnostic)));

    /// <summary>
    /// Gets total bytes across inspected files.
    /// </summary>
    public long TotalSizeBytes => Files.Sum(file => file.SizeBytes);

    /// <summary>
    /// Gets total compressed record count across inspected Archive II files.
    /// </summary>
    public long TotalCompressedRecordCount => Files.Sum(file => file.CompressedRecords.Count);

    /// <summary>
    /// Gets total compressed payload bytes across inspected compressed records.
    /// </summary>
    public long TotalCompressedBytes =>
        Files.Sum(file => file.CompressedRecords.Sum(record => (long)record.CompressedSizeBytes));

    /// <summary>
    /// Gets total compressed records whose payload starts with a BZip2 signature.
    /// </summary>
    public long TotalRecordsWithBZip2Signature =>
        Files.Sum(file => file.CompressedRecords.Count(record => record.StartsWithBZip2Signature));

    /// <summary>
    /// Gets total compressed records that were successfully decompressed during inspection.
    /// </summary>
    public long TotalDecompressedRecordCount =>
        Files.Sum(file => file.CompressedRecords.Count(record => record.DecompressedSizeBytes is not null));

    /// <summary>
    /// Gets total decompressed bytes observed during inspection.
    /// </summary>
    public long TotalDecompressedBytes =>
        Files.Sum(file => file.CompressedRecords.Sum(record => record.DecompressedSizeBytes ?? 0L));

    /// <summary>
    /// Gets total RDA/RPG messages discovered in inspected Archive II data.
    /// </summary>
    public long TotalMessages => Files.Sum(file => file.MessageSummary?.MessageCount ?? 0);

    /// <summary>
    /// Gets total type 31 radials discovered in inspected Archive II data.
    /// </summary>
    public long TotalType31Radials => Files.Sum(file => file.MessageSummary?.Type31.RadialCount ?? 0);

    /// <summary>
    /// Gets estimated gate-moment events represented by inspected type 31 moments.
    /// </summary>
    public long TotalEstimatedGateMomentEvents =>
        Files.Sum(file => file.MessageSummary?.Type31.EstimatedGateMomentEventCount ?? 0);
}
