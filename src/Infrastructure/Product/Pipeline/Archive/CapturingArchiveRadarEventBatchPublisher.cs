using RadarPulse.Application.Archive;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Product;

internal sealed class CapturingArchiveRadarEventBatchPublisher : IArchiveRadarEventBatchPublisher
{
    private readonly List<RadarEventBatch> batches = new();

    public IReadOnlyList<RadarEventBatch> Batches => batches;

    public void Publish(
        RadarEventBatch batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();
        batches.Add(batch.ToOwnedSnapshot());
    }
}
