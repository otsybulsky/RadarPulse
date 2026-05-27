namespace RadarPulse.Domain.Archive;

/// <summary>
/// Validation result comparing sequential and parallel Archive II replay shape.
/// </summary>
public sealed record ArchiveTwoReplayShapeValidationResult(
    string Decompressor,
    int DegreeOfParallelism,
    int ExaminedFileCount,
    int SkippedFileCount,
    IReadOnlyList<ArchiveTwoReplayShapeValidationFileResult> Files)
{
    /// <summary>
    /// Gets the number of Archive II files compared.
    /// </summary>
    public int ComparedFileCount => Files.Count;

    /// <summary>
    /// Gets the number of compared files with a replay-shape diagnostic.
    /// </summary>
    public int FailedFileCount => Files.Count(file => !file.Succeeded);

    /// <summary>
    /// Gets total compressed records from sequential comparison metrics.
    /// </summary>
    public long TotalCompressedRecordCount => Files.Sum(file => file.Sequential.CompressedRecordCount);

    /// <summary>
    /// Gets total compressed bytes from sequential comparison metrics.
    /// </summary>
    public long TotalCompressedBytes => Files.Sum(file => file.Sequential.CompressedBytes);

    /// <summary>
    /// Gets total decompressed bytes from sequential comparison metrics.
    /// </summary>
    public long TotalDecompressedBytes => Files.Sum(file => file.Sequential.DecompressedBytes);

    /// <summary>
    /// Gets total projected events from sequential comparison metrics.
    /// </summary>
    public long TotalEvents => Files.Sum(file => file.Sequential.Events);

    /// <summary>
    /// Gets total valid projected events from sequential comparison metrics.
    /// </summary>
    public long TotalValidEvents => Files.Sum(file => file.Sequential.ValidEvents);

    /// <summary>
    /// Gets total below-threshold projected events from sequential comparison metrics.
    /// </summary>
    public long TotalBelowThresholdEvents => Files.Sum(file => file.Sequential.BelowThresholdEvents);

    /// <summary>
    /// Gets total range-folded projected events from sequential comparison metrics.
    /// </summary>
    public long TotalRangeFoldedEvents => Files.Sum(file => file.Sequential.RangeFoldedEvents);

    /// <summary>
    /// Gets total clutter-filter-not-applied projected events from sequential comparison metrics.
    /// </summary>
    public long TotalClutterFilterNotAppliedEvents => Files.Sum(file => file.Sequential.ClutterFilterNotAppliedEvents);

    /// <summary>
    /// Gets total point-clutter-filter-applied projected events from sequential comparison metrics.
    /// </summary>
    public long TotalPointClutterFilterAppliedEvents => Files.Sum(file => file.Sequential.PointClutterFilterAppliedEvents);

    /// <summary>
    /// Gets total dual-polarization-filtered projected events from sequential comparison metrics.
    /// </summary>
    public long TotalDualPolarizationFilteredEvents => Files.Sum(file => file.Sequential.DualPolarizationFilteredEvents);

    /// <summary>
    /// Gets total reserved projected events from sequential comparison metrics.
    /// </summary>
    public long TotalReservedEvents => Files.Sum(file => file.Sequential.ReservedEvents);

    /// <summary>
    /// Gets total unsupported projected events from sequential comparison metrics.
    /// </summary>
    public long TotalUnsupportedEvents => Files.Sum(file => file.Sequential.UnsupportedEvents);

    /// <summary>
    /// Gets whether at least one file was compared and none failed.
    /// </summary>
    public bool Succeeded => ComparedFileCount > 0 && FailedFileCount == 0;

    /// <summary>
    /// Gets the valid-event share across all compared sequential metrics.
    /// </summary>
    public double ValidEventShare =>
        TotalEvents == 0
            ? 0
            : TotalValidEvents / (double)TotalEvents;
}

/// <summary>
/// Per-file replay-shape comparison between sequential and parallel projection.
/// </summary>
public sealed record ArchiveTwoReplayShapeValidationFileResult(
    string FilePath,
    ArchiveTwoReplayShapeValidationMetrics Sequential,
    ArchiveTwoReplayShapeValidationMetrics Parallel,
    ArchiveTwoReplayShapeUnevennessSummary RecordUnevenness,
    ArchiveTwoReplayShapeUnevennessSummary SweepUnevenness,
    ArchiveTwoReplayShapeUnevennessSummary RadialUnevenness,
    ArchiveTwoReplayShapeUnevennessSummary TimeBucketUnevenness,
    string? Diagnostic)
{
    /// <summary>
    /// Gets whether the file comparison completed without a diagnostic.
    /// </summary>
    public bool Succeeded => Diagnostic is null;
}

/// <summary>
/// Deterministic replay metrics for one projection mode.
/// </summary>
public sealed record ArchiveTwoReplayShapeValidationMetrics(
    int CompressedRecordCount,
    long CompressedBytes,
    long DecompressedBytes,
    long Events,
    long ValidEvents,
    long BelowThresholdEvents,
    long RangeFoldedEvents,
    long ClutterFilterNotAppliedEvents,
    long PointClutterFilterAppliedEvents,
    long DualPolarizationFilteredEvents,
    long ReservedEvents,
    long UnsupportedEvents,
    long RawValueChecksum,
    long CalibratedValueScaledChecksum,
    ulong ChronologyChecksum)
{
    /// <summary>
    /// Gets the valid-event share for this metric set.
    /// </summary>
    public double ValidEventShare =>
        Events == 0
            ? 0
            : ValidEvents / (double)Events;
}

/// <summary>
/// Minimum and maximum valid-event distribution buckets for a replay-shape partitioning strategy.
/// </summary>
public sealed record ArchiveTwoReplayShapeUnevennessSummary(
    string BucketKind,
    int BucketCount,
    ArchiveTwoReplayShapeUnevennessBucket MinimumValidShareBucket,
    ArchiveTwoReplayShapeUnevennessBucket MaximumValidShareBucket,
    ArchiveTwoReplayShapeUnevennessBucket MinimumValidEventsBucket,
    ArchiveTwoReplayShapeUnevennessBucket MaximumValidEventsBucket)
{
    /// <summary>
    /// Creates an empty unevenness summary for a bucket kind with no observed events.
    /// </summary>
    public static ArchiveTwoReplayShapeUnevennessSummary Empty(string bucketKind)
    {
        var emptyBucket = new ArchiveTwoReplayShapeUnevennessBucket(0, 0, 0);
        return new ArchiveTwoReplayShapeUnevennessSummary(
            bucketKind,
            BucketCount: 0,
            emptyBucket,
            emptyBucket,
            emptyBucket,
            emptyBucket);
    }
}

/// <summary>
/// Event and valid-event counts for one replay-shape unevenness bucket.
/// </summary>
public sealed record ArchiveTwoReplayShapeUnevennessBucket(
    int BucketNumber,
    long Events,
    long ValidEvents)
{
    /// <summary>
    /// Gets the valid-event share for this bucket.
    /// </summary>
    public double ValidEventShare =>
        Events == 0
            ? 0
            : ValidEvents / (double)Events;
}
