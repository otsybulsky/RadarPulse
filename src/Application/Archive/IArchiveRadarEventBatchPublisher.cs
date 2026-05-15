using RadarPulse.Domain.Streaming;

namespace RadarPulse.Application.Archive;

public interface IArchiveRadarEventBatchPublisher
{
    void Publish(RadarEventBatch batch, CancellationToken cancellationToken);
}
