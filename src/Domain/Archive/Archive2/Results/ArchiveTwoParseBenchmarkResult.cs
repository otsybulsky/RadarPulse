namespace RadarPulse.Domain.Archive;

/// <summary>
/// Timing, allocation, and deterministic totals for repeated Archive II message parsing.
/// </summary>
public sealed record ArchiveTwoParseBenchmarkResult(
    string FilePath,
    string Decompressor,
    int Iterations,
    int WarmupIterations,
    int DegreeOfParallelism,
    bool DecodeMomentValues,
    bool DecodeCalibratedMomentValues,
    long FileSizeBytes,
    int CompressedRecordsPerIteration,
    long CompressedBytesPerIteration,
    long DecompressedBytesPerIteration,
    int MessagesPerIteration,
    int Type31RadialsPerIteration,
    long EstimatedGateMomentEventsPerIteration,
    long DecodedGateMomentValuesPerIteration,
    ulong DecodedGateMomentValueChecksumPerIteration,
    long CalibratedGateMomentValuesPerIteration,
    long BelowThresholdGateMomentValuesPerIteration,
    long RangeFoldedGateMomentValuesPerIteration,
    long ClutterFilterNotAppliedGateMomentValuesPerIteration,
    long PointClutterFilterAppliedGateMomentValuesPerIteration,
    long DualPolarizationFilteredGateMomentValuesPerIteration,
    long ReservedGateMomentValuesPerIteration,
    long UnsupportedCalibratedGateMomentValuesPerIteration,
    long CalibratedGateMomentValueScaledChecksumPerIteration,
    double MinimumCalibratedGateMomentValuePerIteration,
    double MaximumCalibratedGateMomentValuePerIteration,
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
    /// Gets total RDA/RPG messages parsed across measured iterations.
    /// </summary>
    public long TotalMessages => (long)MessagesPerIteration * Iterations;

    /// <summary>
    /// Gets total type 31 radials parsed across measured iterations.
    /// </summary>
    public long TotalType31Radials => (long)Type31RadialsPerIteration * Iterations;

    /// <summary>
    /// Gets total estimated gate-moment events across measured iterations.
    /// </summary>
    public long TotalEstimatedGateMomentEvents => EstimatedGateMomentEventsPerIteration * Iterations;

    /// <summary>
    /// Gets total decoded raw gate-moment values across measured iterations.
    /// </summary>
    public long TotalDecodedGateMomentValues => DecodedGateMomentValuesPerIteration * Iterations;

    /// <summary>
    /// Gets total calibrated gate-moment values across measured iterations.
    /// </summary>
    public long TotalCalibratedGateMomentValues => CalibratedGateMomentValuesPerIteration * Iterations;

    /// <summary>
    /// Gets total below-threshold gate values across measured iterations.
    /// </summary>
    public long TotalBelowThresholdGateMomentValues => BelowThresholdGateMomentValuesPerIteration * Iterations;

    /// <summary>
    /// Gets total range-folded gate values across measured iterations.
    /// </summary>
    public long TotalRangeFoldedGateMomentValues => RangeFoldedGateMomentValuesPerIteration * Iterations;

    /// <summary>
    /// Gets total clutter-filter-not-applied gate values across measured iterations.
    /// </summary>
    public long TotalClutterFilterNotAppliedGateMomentValues => ClutterFilterNotAppliedGateMomentValuesPerIteration * Iterations;

    /// <summary>
    /// Gets total point-clutter-filter-applied gate values across measured iterations.
    /// </summary>
    public long TotalPointClutterFilterAppliedGateMomentValues => PointClutterFilterAppliedGateMomentValuesPerIteration * Iterations;

    /// <summary>
    /// Gets total dual-polarization-filtered gate values across measured iterations.
    /// </summary>
    public long TotalDualPolarizationFilteredGateMomentValues => DualPolarizationFilteredGateMomentValuesPerIteration * Iterations;

    /// <summary>
    /// Gets total reserved gate values across measured iterations.
    /// </summary>
    public long TotalReservedGateMomentValues => ReservedGateMomentValuesPerIteration * Iterations;

    /// <summary>
    /// Gets total unsupported calibrated gate values across measured iterations.
    /// </summary>
    public long TotalUnsupportedCalibratedGateMomentValues => UnsupportedCalibratedGateMomentValuesPerIteration * Iterations;
}
