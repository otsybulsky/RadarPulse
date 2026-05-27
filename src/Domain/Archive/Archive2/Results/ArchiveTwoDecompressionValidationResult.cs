namespace RadarPulse.Domain.Archive;

/// <summary>
/// Comparison result between a candidate and reference Archive II BZip2 decompressor.
/// </summary>
public sealed record ArchiveTwoDecompressionValidationResult(
    string CandidateDecompressor,
    string ReferenceDecompressor,
    int ExaminedFileCount,
    int SkippedFileCount,
    IReadOnlyList<ArchiveTwoDecompressionValidationFileResult> Files)
{
    /// <summary>
    /// Gets the number of Archive II files compared by both decompressors.
    /// </summary>
    public int ComparedFileCount => Files.Count;

    /// <summary>
    /// Gets the number of compared files whose decompression result differed or failed.
    /// </summary>
    public int FailedFileCount => Files.Count(file => !file.Succeeded);

    /// <summary>
    /// Gets total compared compressed records.
    /// </summary>
    public int TotalCompressedRecordCount => Files.Sum(file => file.CompressedRecordCount);

    /// <summary>
    /// Gets total compared compressed bytes.
    /// </summary>
    public long TotalCompressedBytes => Files.Sum(file => file.CompressedBytes);

    /// <summary>
    /// Gets total decompressed bytes produced by successful comparisons.
    /// </summary>
    public long TotalDecompressedBytes => Files.Sum(file => file.DecompressedBytes);

    /// <summary>
    /// Gets whether at least one file was compared and none failed.
    /// </summary>
    public bool Succeeded => ComparedFileCount > 0 && FailedFileCount == 0;
}

/// <summary>
/// Per-file decompressor comparison result.
/// </summary>
public sealed record ArchiveTwoDecompressionValidationFileResult(
    string FilePath,
    int CompressedRecordCount,
    long CompressedBytes,
    long DecompressedBytes,
    string? Diagnostic)
{
    /// <summary>
    /// Gets whether the file comparison completed without a diagnostic.
    /// </summary>
    public bool Succeeded => Diagnostic is null;
}
