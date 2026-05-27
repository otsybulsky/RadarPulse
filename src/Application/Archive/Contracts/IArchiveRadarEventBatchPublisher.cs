using RadarPulse.Domain.Streaming;

namespace RadarPulse.Application.Archive;

public interface IArchiveRadarEventBatchPublisher
{
    // Leased batches are only valid for the duration of this synchronous call.
    // Publishers that need to retain data must call RadarEventBatch.ToOwnedSnapshot().
    void Publish(RadarEventBatch batch, CancellationToken cancellationToken);
}
