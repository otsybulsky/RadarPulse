namespace RadarPulse.Domain.Archive;

/// <summary>
/// Timing, allocation, and replay-shape totals for repeated Archive II gate-moment projection.
/// </summary>
public sealed record ArchiveTwoReplayShapeBenchmarkResult(
    string FilePath,
    string Decompressor,
    int Iterations,
    int WarmupIterations,
    int DegreeOfParallelism,
    long FileSizeBytes,
    int CompressedRecordsPerIteration,
    long CompressedBytesPerIteration,
    long DecompressedBytesPerIteration,
    long EventsPerIteration,
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
    double MinimumCalibratedValuePerIteration,
    double MaximumCalibratedValuePerIteration,
    double MinimumRangeKilometersPerIteration,
    double MaximumRangeKilometersPerIteration,
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
    /// Gets total projected gate-moment events across measured iterations.
    /// </summary>
    public long TotalEvents => EventsPerIteration * Iterations;

    /// <summary>
    /// Gets total valid projected events across measured iterations.
    /// </summary>
    public long TotalValidEvents => ValidEventsPerIteration * Iterations;

    /// <summary>
    /// Gets total below-threshold projected events across measured iterations.
    /// </summary>
    public long TotalBelowThresholdEvents => BelowThresholdEventsPerIteration * Iterations;

    /// <summary>
    /// Gets total range-folded projected events across measured iterations.
    /// </summary>
    public long TotalRangeFoldedEvents => RangeFoldedEventsPerIteration * Iterations;

    /// <summary>
    /// Gets total clutter-filter-not-applied projected events across measured iterations.
    /// </summary>
    public long TotalClutterFilterNotAppliedEvents => ClutterFilterNotAppliedEventsPerIteration * Iterations;

    /// <summary>
    /// Gets total point-clutter-filter-applied projected events across measured iterations.
    /// </summary>
    public long TotalPointClutterFilterAppliedEvents => PointClutterFilterAppliedEventsPerIteration * Iterations;

    /// <summary>
    /// Gets total dual-polarization-filtered projected events across measured iterations.
    /// </summary>
    public long TotalDualPolarizationFilteredEvents => DualPolarizationFilteredEventsPerIteration * Iterations;

    /// <summary>
    /// Gets total reserved projected events across measured iterations.
    /// </summary>
    public long TotalReservedEvents => ReservedEventsPerIteration * Iterations;

    /// <summary>
    /// Gets total unsupported projected events across measured iterations.
    /// </summary>
    public long TotalUnsupportedEvents => UnsupportedEventsPerIteration * Iterations;
}
