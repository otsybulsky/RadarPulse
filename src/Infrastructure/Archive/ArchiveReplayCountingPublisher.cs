using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class ArchiveReplayCountingPublisher : IArchiveReplayEventPublisher
{
    private readonly IArchiveReplayEventPublisher? innerPublisher;
    private long publishedEvents;
    private long validEvents;
    private long belowThresholdEvents;
    private long rangeFoldedEvents;
    private long clutterFilterNotAppliedEvents;
    private long pointClutterFilterAppliedEvents;
    private long dualPolarizationFilteredEvents;
    private long reservedEvents;
    private long unsupportedEvents;
    private long rawValueChecksum;
    private long calibratedValueScaledChecksum;
    private ulong chronologyChecksum;

    public ArchiveReplayCountingPublisher()
    {
    }

    public ArchiveReplayCountingPublisher(IArchiveReplayEventPublisher innerPublisher)
    {
        this.innerPublisher = innerPublisher ?? throw new ArgumentNullException(nameof(innerPublisher));
    }

    public long PublishedEvents => publishedEvents;

    public long ValidEvents => validEvents;

    public long BelowThresholdEvents => belowThresholdEvents;

    public long RangeFoldedEvents => rangeFoldedEvents;

    public long ClutterFilterNotAppliedEvents => clutterFilterNotAppliedEvents;

    public long PointClutterFilterAppliedEvents => pointClutterFilterAppliedEvents;

    public long DualPolarizationFilteredEvents => dualPolarizationFilteredEvents;

    public long ReservedEvents => reservedEvents;

    public long UnsupportedEvents => unsupportedEvents;

    public long RawValueChecksum => rawValueChecksum;

    public long CalibratedValueScaledChecksum => calibratedValueScaledChecksum;

    public ulong ChronologyChecksum => chronologyChecksum;

    public void Publish(ArchiveTwoGateMomentEvent gateMomentEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        innerPublisher?.Publish(gateMomentEvent, cancellationToken);
        AcceptPublishedEvent(gateMomentEvent);
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
            publishedEvents,
            validEvents,
            belowThresholdEvents,
            rangeFoldedEvents,
            clutterFilterNotAppliedEvents,
            pointClutterFilterAppliedEvents,
            dualPolarizationFilteredEvents,
            reservedEvents,
            unsupportedEvents,
            rawValueChecksum,
            calibratedValueScaledChecksum,
            chronologyChecksum);

    private void AcceptPublishedEvent(ArchiveTwoGateMomentEvent gateMomentEvent)
    {
        publishedEvents++;
        rawValueChecksum += gateMomentEvent.RawValue;
        chronologyChecksum = ArchiveTwoGateMomentChronologyChecksum.Append(chronologyChecksum, gateMomentEvent);

        switch (gateMomentEvent.Status)
        {
            case ArchiveTwoGateMomentStatus.Valid:
                validEvents++;
                checked
                {
                    calibratedValueScaledChecksum += (long)Math.Round(
                        gateMomentEvent.CalibratedValue!.Value * 1_000d,
                        MidpointRounding.AwayFromZero);
                }

                break;
            case ArchiveTwoGateMomentStatus.BelowThreshold:
                belowThresholdEvents++;
                break;
            case ArchiveTwoGateMomentStatus.RangeFolded:
                rangeFoldedEvents++;
                break;
            case ArchiveTwoGateMomentStatus.ClutterFilterNotApplied:
                clutterFilterNotAppliedEvents++;
                break;
            case ArchiveTwoGateMomentStatus.PointClutterFilterApplied:
                pointClutterFilterAppliedEvents++;
                break;
            case ArchiveTwoGateMomentStatus.DualPolarizationFiltered:
                dualPolarizationFilteredEvents++;
                break;
            case ArchiveTwoGateMomentStatus.Reserved:
                reservedEvents++;
                break;
            case ArchiveTwoGateMomentStatus.Unsupported:
                unsupportedEvents++;
                break;
        }
    }
}
