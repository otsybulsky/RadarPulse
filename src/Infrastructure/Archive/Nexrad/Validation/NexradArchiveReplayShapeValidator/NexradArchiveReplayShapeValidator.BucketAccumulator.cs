using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveReplayShapeValidator
{
    private sealed class BucketAccumulator
    {
        private long events;
        private long validEvents;

        public BucketAccumulator(int bucketNumber)
        {
            BucketNumber = bucketNumber;
        }

        private int BucketNumber { get; }

        public void Accept(ArchiveTwoGateMomentEvent gateMomentEvent)
        {
            events++;
            if (gateMomentEvent.Status == ArchiveTwoGateMomentStatus.Valid)
            {
                validEvents++;
            }
        }

        public ArchiveTwoReplayShapeUnevennessBucket Build() =>
            new(BucketNumber, events, validEvents);
    }
}
