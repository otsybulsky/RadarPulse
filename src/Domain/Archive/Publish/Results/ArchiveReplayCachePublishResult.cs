namespace RadarPulse.Domain.Archive;

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
    public int PublishedFileCount => Files.Count;

    public long TotalFileSizeBytes => Files.Sum(file => file.FileSizeBytes);

    public long TotalCompressedRecordCount => Files.Sum(file => (long)file.CompressedRecordCount);

    public long TotalCompressedBytes => Files.Sum(file => file.CompressedBytes);

    public long TotalDecompressedBytes => Files.Sum(file => file.DecompressedBytes);

    public long TotalPublishedEvents => Files.Sum(file => file.PublishedEvents);

    public long TotalValidEvents => Files.Sum(file => file.ValidEvents);

    public long TotalBelowThresholdEvents => Files.Sum(file => file.BelowThresholdEvents);

    public long TotalRangeFoldedEvents => Files.Sum(file => file.RangeFoldedEvents);

    public long TotalClutterFilterNotAppliedEvents => Files.Sum(file => file.ClutterFilterNotAppliedEvents);

    public long TotalPointClutterFilterAppliedEvents => Files.Sum(file => file.PointClutterFilterAppliedEvents);

    public long TotalDualPolarizationFilteredEvents => Files.Sum(file => file.DualPolarizationFilteredEvents);

    public long TotalReservedEvents => Files.Sum(file => file.ReservedEvents);

    public long TotalUnsupportedEvents => Files.Sum(file => file.UnsupportedEvents);

    public long TotalRawValueChecksum => Files.Sum(file => file.RawValueChecksum);

    public long TotalCalibratedValueScaledChecksum => Files.Sum(file => file.CalibratedValueScaledChecksum);

    public double ValidEventShare =>
        TotalPublishedEvents == 0
            ? 0
            : TotalValidEvents / (double)TotalPublishedEvents;
}
