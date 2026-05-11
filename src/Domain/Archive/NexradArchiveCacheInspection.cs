namespace RadarPulse.Domain.Archive;

public sealed record NexradArchiveCacheInspection(
    string CachePath,
    DateOnly? Date,
    string? RadarId,
    int ExaminedFileCount,
    IReadOnlyList<NexradArchiveFileInspection> Files)
{
    public int ArchiveTwoBaseDataFileCount =>
        Files.Count(file => file.FileKind == NexradArchiveFileKind.ArchiveTwoBaseData);

    public int MdmOrCompressedStreamFileCount =>
        Files.Count(file => file.FileKind == NexradArchiveFileKind.MdmOrCompressedStream);

    public int UnknownFileCount =>
        Files.Count(file => file.FileKind == NexradArchiveFileKind.Unknown);

    public int DiagnosticFileCount =>
        Files.Count(file =>
            !string.IsNullOrWhiteSpace(file.Diagnostic) ||
            file.CompressedRecords.Any(record => !string.IsNullOrWhiteSpace(record.DecompressionDiagnostic)));

    public long TotalSizeBytes => Files.Sum(file => file.SizeBytes);

    public long TotalCompressedRecordCount => Files.Sum(file => file.CompressedRecords.Count);

    public long TotalCompressedBytes =>
        Files.Sum(file => file.CompressedRecords.Sum(record => (long)record.CompressedSizeBytes));

    public long TotalRecordsWithBZip2Signature =>
        Files.Sum(file => file.CompressedRecords.Count(record => record.StartsWithBZip2Signature));

    public long TotalDecompressedRecordCount =>
        Files.Sum(file => file.CompressedRecords.Count(record => record.DecompressedSizeBytes is not null));

    public long TotalDecompressedBytes =>
        Files.Sum(file => file.CompressedRecords.Sum(record => record.DecompressedSizeBytes ?? 0L));

    public long TotalMessages => Files.Sum(file => file.MessageSummary?.MessageCount ?? 0);

    public long TotalType31Radials => Files.Sum(file => file.MessageSummary?.Type31.RadialCount ?? 0);

    public long TotalEstimatedGateMomentEvents =>
        Files.Sum(file => file.MessageSummary?.Type31.EstimatedGateMomentEventCount ?? 0);
}
