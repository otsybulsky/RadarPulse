using RadarPulse.Domain.Archive;

namespace RadarPulse.Application.Archive;

public interface IArchiveReplayEventPublisher
{
    void Publish(ArchiveTwoGateMomentEvent gateMomentEvent, CancellationToken cancellationToken);
}
