namespace RadarPulse.Domain.Archive;

public sealed record ArchiveTwoReplayShapeValidationResult(
    string Decompressor,
    int DegreeOfParallelism,
    int ExaminedFileCount,
    int SkippedFileCount,
    IReadOnlyList<ArchiveTwoReplayShapeValidationFileResult> Files)
{
    public int ComparedFileCount => Files.Count;

    public int FailedFileCount => Files.Count(file => !file.Succeeded);

    public long TotalCompressedRecordCount => Files.Sum(file => file.Sequential.CompressedRecordCount);

    public long TotalCompressedBytes => Files.Sum(file => file.Sequential.CompressedBytes);

    public long TotalDecompressedBytes => Files.Sum(file => file.Sequential.DecompressedBytes);

    public long TotalEvents => Files.Sum(file => file.Sequential.Events);

    public long TotalValidEvents => Files.Sum(file => file.Sequential.ValidEvents);

    public long TotalBelowThresholdEvents => Files.Sum(file => file.Sequential.BelowThresholdEvents);

    public long TotalRangeFoldedEvents => Files.Sum(file => file.Sequential.RangeFoldedEvents);

    public long TotalClutterFilterNotAppliedEvents => Files.Sum(file => file.Sequential.ClutterFilterNotAppliedEvents);

    public long TotalPointClutterFilterAppliedEvents => Files.Sum(file => file.Sequential.PointClutterFilterAppliedEvents);

    public long TotalDualPolarizationFilteredEvents => Files.Sum(file => file.Sequential.DualPolarizationFilteredEvents);

    public long TotalReservedEvents => Files.Sum(file => file.Sequential.ReservedEvents);

    public long TotalUnsupportedEvents => Files.Sum(file => file.Sequential.UnsupportedEvents);

    public bool Succeeded => ComparedFileCount > 0 && FailedFileCount == 0;

    public double ValidEventShare =>
        TotalEvents == 0
            ? 0
            : TotalValidEvents / (double)TotalEvents;
}

public sealed record ArchiveTwoReplayShapeValidationFileResult(
    string FilePath,
    ArchiveTwoReplayShapeValidationMetrics Sequential,
    ArchiveTwoReplayShapeValidationMetrics Parallel,
    ArchiveTwoReplayShapeUnevennessSummary RecordUnevenness,
    ArchiveTwoReplayShapeUnevennessSummary SweepUnevenness,
    string? Diagnostic)
{
    public bool Succeeded => Diagnostic is null;
}

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
    public double ValidEventShare =>
        Events == 0
            ? 0
            : ValidEvents / (double)Events;
}

public sealed record ArchiveTwoReplayShapeUnevennessSummary(
    string BucketKind,
    int BucketCount,
    ArchiveTwoReplayShapeUnevennessBucket MinimumValidShareBucket,
    ArchiveTwoReplayShapeUnevennessBucket MaximumValidShareBucket,
    ArchiveTwoReplayShapeUnevennessBucket MinimumValidEventsBucket,
    ArchiveTwoReplayShapeUnevennessBucket MaximumValidEventsBucket)
{
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

public sealed record ArchiveTwoReplayShapeUnevennessBucket(
    int BucketNumber,
    long Events,
    long ValidEvents)
{
    public double ValidEventShare =>
        Events == 0
            ? 0
            : ValidEvents / (double)Events;
}
