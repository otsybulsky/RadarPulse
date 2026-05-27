namespace RadarPulse.Domain.Archive;

public sealed record ArchiveTwoDecompressionValidationResult(
    string CandidateDecompressor,
    string ReferenceDecompressor,
    int ExaminedFileCount,
    int SkippedFileCount,
    IReadOnlyList<ArchiveTwoDecompressionValidationFileResult> Files)
{
    public int ComparedFileCount => Files.Count;

    public int FailedFileCount => Files.Count(file => !file.Succeeded);

    public int TotalCompressedRecordCount => Files.Sum(file => file.CompressedRecordCount);

    public long TotalCompressedBytes => Files.Sum(file => file.CompressedBytes);

    public long TotalDecompressedBytes => Files.Sum(file => file.DecompressedBytes);

    public bool Succeeded => ComparedFileCount > 0 && FailedFileCount == 0;
}

public sealed record ArchiveTwoDecompressionValidationFileResult(
    string FilePath,
    int CompressedRecordCount,
    long CompressedBytes,
    long DecompressedBytes,
    string? Diagnostic)
{
    public bool Succeeded => Diagnostic is null;
}
