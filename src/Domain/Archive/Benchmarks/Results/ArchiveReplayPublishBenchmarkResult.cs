namespace RadarPulse.Domain.Archive;

/// <summary>
/// Benchmark result for repeated Archive II replay publishing over one file.
/// </summary>
public sealed record ArchiveReplayPublishBenchmarkResult(
    string FilePath,
    string Decompressor,
    int Iterations,
    int WarmupIterations,
    int DegreeOfParallelism,
    long FileSizeBytes,
    int CompressedRecordsPerIteration,
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
    /// Gets total compressed records processed across measured iterations.
    /// </summary>
    public long TotalCompressedRecords => (long)CompressedRecordsPerIteration * Iterations;

    /// <summary>
    /// Gets total compressed payload bytes processed across measured iterations.
    /// </summary>
    public long TotalCompressedBytes => CompressedBytesPerIteration * Iterations;

    /// <summary>
    /// Gets total decompressed bytes produced across measured iterations.
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
