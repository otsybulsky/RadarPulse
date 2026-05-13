using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class ArchiveReplayCountingPublisher : IArchiveReplayEventPublisher
{
    private readonly IArchiveReplayEventPublisher? innerPublisher;
    private readonly ArchiveReplayEventAccumulator accumulator = new();

    public ArchiveReplayCountingPublisher()
    {
    }

    public ArchiveReplayCountingPublisher(IArchiveReplayEventPublisher innerPublisher)
    {
        this.innerPublisher = innerPublisher ?? throw new ArgumentNullException(nameof(innerPublisher));
    }

    public long PublishedEvents => accumulator.PublishedEvents;

    public long ValidEvents => accumulator.ValidEvents;

    public long BelowThresholdEvents => accumulator.BelowThresholdEvents;

    public long RangeFoldedEvents => accumulator.RangeFoldedEvents;

    public long ClutterFilterNotAppliedEvents => accumulator.ClutterFilterNotAppliedEvents;

    public long PointClutterFilterAppliedEvents => accumulator.PointClutterFilterAppliedEvents;

    public long DualPolarizationFilteredEvents => accumulator.DualPolarizationFilteredEvents;

    public long ReservedEvents => accumulator.ReservedEvents;

    public long UnsupportedEvents => accumulator.UnsupportedEvents;

    public long RawValueChecksum => accumulator.RawValueChecksum;

    public long CalibratedValueScaledChecksum => accumulator.CalibratedValueScaledChecksum;

    public ulong ChronologyChecksum => accumulator.ChronologyChecksum;

    public void Publish(ArchiveTwoGateMomentEvent gateMomentEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        innerPublisher?.Publish(gateMomentEvent, cancellationToken);
        accumulator.AcceptEvent(gateMomentEvent);
    }

    public ArchiveReplayPublishResult BuildResult(
        string filePath,
        string decompressor,
        int degreeOfParallelism,
        long fileSizeBytes,
        int compressedRecordCount,
        long compressedBytes,
        long decompressedBytes) =>
        accumulator.BuildResult(
            filePath,
            decompressor,
            degreeOfParallelism,
            fileSizeBytes,
            compressedRecordCount,
            compressedBytes,
            decompressedBytes);
}
