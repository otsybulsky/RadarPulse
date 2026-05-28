using System.Buffers.Binary;
using System.Text;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class NexradArchiveReplayPublisherTests
{
    private sealed class CapturingReplayPublisher : IArchiveReplayEventPublisher
    {
        private readonly List<ArchiveTwoGateMomentEvent> events = new();

        public IReadOnlyList<ArchiveTwoGateMomentEvent> Events => events;

        public void Publish(ArchiveTwoGateMomentEvent gateMomentEvent, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            events.Add(gateMomentEvent);
        }
    }

}
