namespace RadarPulse.Domain.Archive;

/// <summary>
/// Benchmark result for repeated Archive II replay publishing over a cache selection.
/// </summary>
public sealed record ArchiveReplayPublishCacheBenchmarkResult(
    string CachePath,
    DateOnly? Date,
    string? RadarId,
    string Decompressor,
    int Iterations,
    int WarmupIterations,
    int DegreeOfParallelism,
    int ExaminedFilesPerIteration,
    int SkippedFilesPerIteration,
    int PublishedFilesPerIteration,
    long FileSizeBytesPerIteration,
    long CompressedRecordsPerIteration,
    long CompressedBytesPerIteration,
    long DecompressedBytesPerIteration,
    long PublishedEventsPerIteration,
    long ValidEventsPerIteration,
    long BelowThresholdEventsPerIteration,
    long RangeFoldedEventsPerIteration,
    long ClutterFilterNotAppliedEventsPerIteration,
    long PointClutterFilterAppliedEventsPerIteration,
    long DualPolarizationFilteredEventsPerIteration,
    long ReservedEventsPerIteration,
    long UnsupportedEventsPerIteration,
    long RawValueChecksumPerIteration,
    long CalibratedValueScaledChecksumPerIteration,
    ulong ChronologyChecksumPerIteration,
    TimeSpan Elapsed,
    long AllocatedBytes)
{
    /// <summary>
    /// Gets total examined files across measured iterations.
    /// </summary>
    public long TotalExaminedFiles => (long)ExaminedFilesPerIteration * Iterations;

    /// <summary>
    /// Gets total skipped files across measured iterations.
    /// </summary>
    public long TotalSkippedFiles => (long)SkippedFilesPerIteration * Iterations;

    /// <summary>
    /// Gets total published files across measured iterations.
    /// </summary>
    public long TotalPublishedFiles => (long)PublishedFilesPerIteration * Iterations;

    /// <summary>
    /// Gets total file size bytes across measured iterations.
    /// </summary>
    public long TotalFileSizeBytes => FileSizeBytesPerIteration * Iterations;

    /// <summary>
    /// Gets total compressed records across measured iterations.
    /// </summary>
    public long TotalCompressedRecords => CompressedRecordsPerIteration * Iterations;

    /// <summary>
    /// Gets total compressed payload bytes across measured iterations.
    /// </summary>
    public long TotalCompressedBytes => CompressedBytesPerIteration * Iterations;

    /// <summary>
    /// Gets total decompressed bytes across measured iterations.
    /// </summary>
    public long TotalDecompressedBytes => DecompressedBytesPerIteration * Iterations;

    /// <summary>
    /// Gets total published replay events across measured iterations.
    /// </summary>
    public long TotalPublishedEvents => PublishedEventsPerIteration * Iterations;

    /// <summary>
    /// Gets total valid replay events across measured iterations.
    /// </summary>
    public long TotalValidEvents => ValidEventsPerIteration * Iterations;

    /// <summary>
    /// Gets total below-threshold replay events across measured iterations.
    /// </summary>
    public long TotalBelowThresholdEvents => BelowThresholdEventsPerIteration * Iterations;

    /// <summary>
    /// Gets total range-folded replay events across measured iterations.
    /// </summary>
    public long TotalRangeFoldedEvents => RangeFoldedEventsPerIteration * Iterations;

    /// <summary>
    /// Gets total clutter-filter-not-applied replay events across measured iterations.
    /// </summary>
    public long TotalClutterFilterNotAppliedEvents => ClutterFilterNotAppliedEventsPerIteration * Iterations;

    /// <summary>
    /// Gets total point-clutter-filter-applied replay events across measured iterations.
    /// </summary>
    public long TotalPointClutterFilterAppliedEvents => PointClutterFilterAppliedEventsPerIteration * Iterations;

    /// <summary>
    /// Gets total dual-polarization-filtered replay events across measured iterations.
    /// </summary>
    public long TotalDualPolarizationFilteredEvents => DualPolarizationFilteredEventsPerIteration * Iterations;

    /// <summary>
    /// Gets total reserved replay events across measured iterations.
    /// </summary>
    public long TotalReservedEvents => ReservedEventsPerIteration * Iterations;

    /// <summary>
    /// Gets total unsupported replay events across measured iterations.
    /// </summary>
    public long TotalUnsupportedEvents => UnsupportedEventsPerIteration * Iterations;
}
