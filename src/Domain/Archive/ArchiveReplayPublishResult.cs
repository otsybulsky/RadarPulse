namespace RadarPulse.Domain.Archive;

public sealed record ArchiveReplayPublishResult(
    string FilePath,
    string Decompressor,
    int DegreeOfParallelism,
    long FileSizeBytes,
    int CompressedRecordCount,
    long CompressedBytes,
    long DecompressedBytes,
    long PublishedEvents,
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
        PublishedEvents == 0
            ? 0
            : ValidEvents / (double)PublishedEvents;
}
