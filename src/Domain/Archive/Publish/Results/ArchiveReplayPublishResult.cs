namespace RadarPulse.Domain.Archive;

/// <summary>
/// Result for publishing gate-moment replay events from one Archive II file.
/// </summary>
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
    /// <summary>
    /// Gets the valid-event share among all published events.
    /// </summary>
    public double ValidEventShare =>
        PublishedEvents == 0
            ? 0
            : ValidEvents / (double)PublishedEvents;
}
