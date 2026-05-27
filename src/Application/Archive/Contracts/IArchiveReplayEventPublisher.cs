using RadarPulse.Domain.Archive;

namespace RadarPulse.Application.Archive;

/// <summary>
/// Publishes individual Archive II gate-moment events produced by replay projection.
/// </summary>
public interface IArchiveReplayEventPublisher
{
    /// <summary>
    /// Publishes one projected gate-moment event in replay order.
    /// </summary>
    void Publish(ArchiveTwoGateMomentEvent gateMomentEvent, CancellationToken cancellationToken);
}
