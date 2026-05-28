using System.Buffers.Binary;
using System.Text;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Archive;

public sealed partial class NexradArchiveRadarEventBatchPublisherTests
{
    [Fact]
    public void PublishOptionsRejectInvalidParallelism()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ArchiveRadarEventBatchPublishOptions(
                ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse,
                degreeOfParallelism: 0));
    }
}
