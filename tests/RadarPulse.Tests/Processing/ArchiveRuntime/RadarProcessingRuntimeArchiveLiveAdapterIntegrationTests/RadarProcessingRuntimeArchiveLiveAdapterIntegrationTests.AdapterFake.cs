using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests
{
    private sealed class DeterministicArchiveLiveAdapter
    {
        private readonly RadarSourceUniverse universe;
        private readonly IReadOnlyList<RadarEventBatch> batches;

        public DeterministicArchiveLiveAdapter(
            RadarSourceUniverse universe,
            IReadOnlyList<RadarEventBatch> batches)
        {
            this.universe = universe ?? throw new ArgumentNullException(nameof(universe));
            this.batches = batches ?? throw new ArgumentNullException(nameof(batches));
        }

        public ArchiveRadarEventBatchPublishResult PublishTo(
            IArchiveRadarEventBatchPublisher publisher,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(publisher);

            foreach (var batch in batches)
            {
                publisher.Publish(batch, cancellationToken);
            }

            return CreatePublishResult(universe, batches);
        }
    }
}
