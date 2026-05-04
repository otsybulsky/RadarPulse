using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Tests.Archive;

public sealed class NexradCachePathMapperTests
{
    [Fact]
    public void MapToLocalPathUsesDeterministicLayout()
    {
        var file = new HistoricalArchiveFile(
            "KTLX",
            new DateOnly(2026, 5, 4),
            NexradArchiveKey.BucketName,
            "2026/05/04/KTLX/KTLX_20260504_120000_V06",
            "KTLX_20260504_120000_V06",
            42,
            DateTimeOffset.UtcNow,
            null);

        var path = new NexradCachePathMapper().MapToLocalPath("data/nexrad", file);

        Assert.Equal(
            Path.Combine("data/nexrad", "level2", "2026", "05", "04", "KTLX", "KTLX_20260504_120000_V06"),
            path);
    }
}
