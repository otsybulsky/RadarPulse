namespace RadarPulse.Domain.Archive;

/// <summary>
/// Aggregate gate-moment replay publish result for a cache scan.
/// </summary>
public sealed record ArchiveReplayCachePublishResult(
    string CachePath,
    DateOnly? Date,
    string? RadarId,
    string Decompressor,
    int DegreeOfParallelism,
    int ExaminedFileCount,
    int SkippedFileCount,
    IReadOnlyList<ArchiveReplayPublishResult> Files,
    ulong ChronologyChecksum)
{
    /// <summary>
    /// Gets the number of Archive II files that produced replay publish results.
    /// </summary>
    public int PublishedFileCount => Files.Count;

    /// <summary>
    /// Gets total file size bytes across published files.
    /// </summary>
    public long TotalFileSizeBytes => Files.Sum(file => file.FileSizeBytes);

    /// <summary>
    /// Gets total compressed records across published files.
    /// </summary>
    public long TotalCompressedRecordCount => Files.Sum(file => (long)file.CompressedRecordCount);

    /// <summary>
    /// Gets total compressed payload bytes across published files.
    /// </summary>
    public long TotalCompressedBytes => Files.Sum(file => file.CompressedBytes);

    /// <summary>
    /// Gets total decompressed bytes across published files.
    /// </summary>
    public long TotalDecompressedBytes => Files.Sum(file => file.DecompressedBytes);

    /// <summary>
    /// Gets total published gate-moment events across files.
    /// </summary>
    public long TotalPublishedEvents => Files.Sum(file => file.PublishedEvents);

    /// <summary>
    /// Gets total valid gate-moment events across files.
    /// </summary>
    public long TotalValidEvents => Files.Sum(file => file.ValidEvents);

    /// <summary>
    /// Gets total below-threshold gate-moment events across files.
    /// </summary>
    public long TotalBelowThresholdEvents => Files.Sum(file => file.BelowThresholdEvents);

    /// <summary>
    /// Gets total range-folded gate-moment events across files.
    /// </summary>
    public long TotalRangeFoldedEvents => Files.Sum(file => file.RangeFoldedEvents);

    /// <summary>
    /// Gets total clutter-filter-not-applied gate-moment events across files.
    /// </summary>
    public long TotalClutterFilterNotAppliedEvents => Files.Sum(file => file.ClutterFilterNotAppliedEvents);

    /// <summary>
    /// Gets total point-clutter-filter-applied gate-moment events across files.
    /// </summary>
    public long TotalPointClutterFilterAppliedEvents => Files.Sum(file => file.PointClutterFilterAppliedEvents);

    /// <summary>
    /// Gets total dual-polarization-filtered gate-moment events across files.
    /// </summary>
    public long TotalDualPolarizationFilteredEvents => Files.Sum(file => file.DualPolarizationFilteredEvents);

    /// <summary>
    /// Gets total reserved gate-moment events across files.
    /// </summary>
    public long TotalReservedEvents => Files.Sum(file => file.ReservedEvents);

    /// <summary>
    /// Gets total unsupported gate-moment events across files.
    /// </summary>
    public long TotalUnsupportedEvents => Files.Sum(file => file.UnsupportedEvents);

    /// <summary>
    /// Gets summed raw value checksums across files.
    /// </summary>
    public long TotalRawValueChecksum => Files.Sum(file => file.RawValueChecksum);

    /// <summary>
    /// Gets summed calibrated-value scaled checksums across files.
    /// </summary>
    public long TotalCalibratedValueScaledChecksum => Files.Sum(file => file.CalibratedValueScaledChecksum);

    /// <summary>
    /// Gets the valid-event share across all published files.
    /// </summary>
    public double ValidEventShare =>
        TotalPublishedEvents == 0
            ? 0
            : TotalValidEvents / (double)TotalPublishedEvents;
}
