using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Replay publisher decorator that counts gate-moment events and deterministic checksums.
/// </summary>
public sealed class ArchiveReplayCountingPublisher : IArchiveReplayEventPublisher
{
    private readonly IArchiveReplayEventPublisher? innerPublisher;
    private readonly ArchiveReplayEventAccumulator accumulator = new();

    /// <summary>
    /// Creates a counting publisher without forwarding events.
    /// </summary>
    public ArchiveReplayCountingPublisher()
    {
    }

    /// <summary>
    /// Creates a counting publisher that forwards events to an inner publisher.
    /// </summary>
    public ArchiveReplayCountingPublisher(IArchiveReplayEventPublisher innerPublisher)
    {
        this.innerPublisher = innerPublisher ?? throw new ArgumentNullException(nameof(innerPublisher));
    }

    /// <summary>
    /// Gets the number of published events observed.
    /// </summary>
    public long PublishedEvents => accumulator.PublishedEvents;

    /// <summary>
    /// Gets the number of valid events observed.
    /// </summary>
    public long ValidEvents => accumulator.ValidEvents;

    /// <summary>
    /// Gets the number of below-threshold events observed.
    /// </summary>
    public long BelowThresholdEvents => accumulator.BelowThresholdEvents;

    /// <summary>
    /// Gets the number of range-folded events observed.
    /// </summary>
    public long RangeFoldedEvents => accumulator.RangeFoldedEvents;

    /// <summary>
    /// Gets the number of clutter-filter-not-applied events observed.
    /// </summary>
    public long ClutterFilterNotAppliedEvents => accumulator.ClutterFilterNotAppliedEvents;

    /// <summary>
    /// Gets the number of point-clutter-filter-applied events observed.
    /// </summary>
    public long PointClutterFilterAppliedEvents => accumulator.PointClutterFilterAppliedEvents;

    /// <summary>
    /// Gets the number of dual-polarization-filtered events observed.
    /// </summary>
    public long DualPolarizationFilteredEvents => accumulator.DualPolarizationFilteredEvents;

    /// <summary>
    /// Gets the number of reserved events observed.
    /// </summary>
    public long ReservedEvents => accumulator.ReservedEvents;

    /// <summary>
    /// Gets the number of unsupported events observed.
    /// </summary>
    public long UnsupportedEvents => accumulator.UnsupportedEvents;

    /// <summary>
    /// Gets the sum of raw event values.
    /// </summary>
    public long RawValueChecksum => accumulator.RawValueChecksum;

    /// <summary>
    /// Gets the sum of calibrated values scaled to thousandths.
    /// </summary>
    public long CalibratedValueScaledChecksum => accumulator.CalibratedValueScaledChecksum;

    /// <summary>
    /// Gets the deterministic ordered chronology checksum.
    /// </summary>
    public ulong ChronologyChecksum => accumulator.ChronologyChecksum;

    /// <inheritdoc />
    public void Publish(ArchiveTwoGateMomentEvent gateMomentEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        innerPublisher?.Publish(gateMomentEvent, cancellationToken);
        accumulator.AcceptEvent(gateMomentEvent);
    }

    /// <summary>
    /// Builds a publish result from observed counts and file metadata.
    /// </summary>
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
