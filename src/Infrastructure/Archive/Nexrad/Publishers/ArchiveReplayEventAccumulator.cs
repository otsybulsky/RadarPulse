using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Accumulates deterministic replay publish totals for ordered gate-moment events.
/// </summary>
internal sealed class ArchiveReplayEventAccumulator
{
    /// <summary>
    /// Gets the number of accepted replay events.
    /// </summary>
    public long PublishedEvents { get; private set; }

    /// <summary>
    /// Gets the number of valid replay events.
    /// </summary>
    public long ValidEvents { get; private set; }

    /// <summary>
    /// Gets the number of below-threshold replay events.
    /// </summary>
    public long BelowThresholdEvents { get; private set; }

    /// <summary>
    /// Gets the number of range-folded replay events.
    /// </summary>
    public long RangeFoldedEvents { get; private set; }

    /// <summary>
    /// Gets the number of clutter-filter-not-applied replay events.
    /// </summary>
    public long ClutterFilterNotAppliedEvents { get; private set; }

    /// <summary>
    /// Gets the number of point-clutter-filter-applied replay events.
    /// </summary>
    public long PointClutterFilterAppliedEvents { get; private set; }

    /// <summary>
    /// Gets the number of dual-polarization-filtered replay events.
    /// </summary>
    public long DualPolarizationFilteredEvents { get; private set; }

    /// <summary>
    /// Gets the number of reserved replay events.
    /// </summary>
    public long ReservedEvents { get; private set; }

    /// <summary>
    /// Gets the number of unsupported replay events.
    /// </summary>
    public long UnsupportedEvents { get; private set; }

    /// <summary>
    /// Gets the sum of raw gate values.
    /// </summary>
    public long RawValueChecksum { get; private set; }

    /// <summary>
    /// Gets the sum of calibrated values scaled to thousandths.
    /// </summary>
    public long CalibratedValueScaledChecksum { get; private set; }

    /// <summary>
    /// Gets the deterministic ordered chronology checksum.
    /// </summary>
    public ulong ChronologyChecksum { get; private set; }

    /// <summary>
    /// Clears accumulated replay totals.
    /// </summary>
    public void Reset()
    {
        PublishedEvents = 0;
        ValidEvents = 0;
        BelowThresholdEvents = 0;
        RangeFoldedEvents = 0;
        ClutterFilterNotAppliedEvents = 0;
        PointClutterFilterAppliedEvents = 0;
        DualPolarizationFilteredEvents = 0;
        ReservedEvents = 0;
        UnsupportedEvents = 0;
        RawValueChecksum = 0;
        CalibratedValueScaledChecksum = 0;
        ChronologyChecksum = 0;
    }

    /// <summary>
    /// Accepts one replay event and updates status counters and checksums.
    /// </summary>
    public void AcceptEvent(ArchiveTwoGateMomentEvent gateMomentEvent)
    {
        PublishedEvents++;
        RawValueChecksum += gateMomentEvent.RawValue;
        ChronologyChecksum = ArchiveTwoGateMomentChronologyChecksum.Append(ChronologyChecksum, gateMomentEvent);

        switch (gateMomentEvent.Status)
        {
            case ArchiveTwoGateMomentStatus.Valid:
                ValidEvents++;
                checked
                {
                    CalibratedValueScaledChecksum += (long)Math.Round(
                        gateMomentEvent.CalibratedValue!.Value * 1_000d,
                        MidpointRounding.AwayFromZero);
                }

                break;
            case ArchiveTwoGateMomentStatus.BelowThreshold:
                BelowThresholdEvents++;
                break;
            case ArchiveTwoGateMomentStatus.RangeFolded:
                RangeFoldedEvents++;
                break;
            case ArchiveTwoGateMomentStatus.ClutterFilterNotApplied:
                ClutterFilterNotAppliedEvents++;
                break;
            case ArchiveTwoGateMomentStatus.PointClutterFilterApplied:
                PointClutterFilterAppliedEvents++;
                break;
            case ArchiveTwoGateMomentStatus.DualPolarizationFiltered:
                DualPolarizationFilteredEvents++;
                break;
            case ArchiveTwoGateMomentStatus.Reserved:
                ReservedEvents++;
                break;
            case ArchiveTwoGateMomentStatus.Unsupported:
                UnsupportedEvents++;
                break;
        }
    }

    /// <summary>
    /// Adds another accumulator as the next ordered segment.
    /// </summary>
    public void AddOrdered(ArchiveReplayEventAccumulator other)
    {
        if (other.PublishedEvents == 0)
        {
            return;
        }

        RawValueChecksum += other.RawValueChecksum;
        CalibratedValueScaledChecksum += other.CalibratedValueScaledChecksum;
        ChronologyChecksum = ArchiveTwoGateMomentChronologyChecksum.Combine(
            ChronologyChecksum,
            other.ChronologyChecksum,
            other.PublishedEvents);

        PublishedEvents += other.PublishedEvents;
        ValidEvents += other.ValidEvents;
        BelowThresholdEvents += other.BelowThresholdEvents;
        RangeFoldedEvents += other.RangeFoldedEvents;
        ClutterFilterNotAppliedEvents += other.ClutterFilterNotAppliedEvents;
        PointClutterFilterAppliedEvents += other.PointClutterFilterAppliedEvents;
        DualPolarizationFilteredEvents += other.DualPolarizationFilteredEvents;
        ReservedEvents += other.ReservedEvents;
        UnsupportedEvents += other.UnsupportedEvents;
    }

    /// <summary>
    /// Builds an immutable publish result from accumulated totals and file metadata.
    /// </summary>
    public ArchiveReplayPublishResult BuildResult(
        string filePath,
        string decompressor,
        int degreeOfParallelism,
        long fileSizeBytes,
        int compressedRecordCount,
        long compressedBytes,
        long decompressedBytes) =>
        new(
            filePath,
            decompressor,
            degreeOfParallelism,
            fileSizeBytes,
            compressedRecordCount,
            compressedBytes,
            decompressedBytes,
            PublishedEvents,
            ValidEvents,
            BelowThresholdEvents,
            RangeFoldedEvents,
            ClutterFilterNotAppliedEvents,
            PointClutterFilterAppliedEvents,
            DualPolarizationFilteredEvents,
            ReservedEvents,
            UnsupportedEvents,
            RawValueChecksum,
            CalibratedValueScaledChecksum,
            ChronologyChecksum);
}
