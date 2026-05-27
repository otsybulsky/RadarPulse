using RadarPulse.Domain.Streaming;

namespace RadarPulse.Application.Archive;

/// <summary>
/// Publishes streaming radar event batches projected from archive files.
/// </summary>
/// <remarks>
/// Implementations may receive leased batches whose backing memory is only valid during the synchronous publish call.
/// Publishers that retain data must convert the batch with <see cref="RadarEventBatch.ToOwnedSnapshot"/>.
/// </remarks>
public interface IArchiveRadarEventBatchPublisher
{
    /// <summary>
    /// Publishes one archive-projected radar event batch.
    /// </summary>
    void Publish(RadarEventBatch batch, CancellationToken cancellationToken);
}
