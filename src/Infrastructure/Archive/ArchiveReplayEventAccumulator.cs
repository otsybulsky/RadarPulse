using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

internal sealed class ArchiveReplayEventAccumulator
{
    public long PublishedEvents { get; private set; }

    public long ValidEvents { get; private set; }

    public long BelowThresholdEvents { get; private set; }

    public long RangeFoldedEvents { get; private set; }

    public long ClutterFilterNotAppliedEvents { get; private set; }

    public long PointClutterFilterAppliedEvents { get; private set; }

    public long DualPolarizationFilteredEvents { get; private set; }

    public long ReservedEvents { get; private set; }

    public long UnsupportedEvents { get; private set; }

    public long RawValueChecksum { get; private set; }

    public long CalibratedValueScaledChecksum { get; private set; }

    public ulong ChronologyChecksum { get; private set; }

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
