using RadarPulse.Application.Archive;

namespace RadarPulse.Tests.Archive;

public sealed class NexradArchiveKeyTests
{
    [Fact]
    public void DatePrefixUsesArchiveLayout()
    {
        Assert.Equal("2026/05/04/", NexradArchiveKey.DatePrefix(new DateOnly(2026, 5, 4)));
    }

    [Fact]
    public void RadarPrefixNormalizesRadarId()
    {
        Assert.Equal("2026/05/04/KTLX/", NexradArchiveKey.RadarPrefix(new DateOnly(2026, 5, 4), "ktlx"));
    }

    [Fact]
    public void ArchiveKeyParsesMetadata()
    {
        var parsed = NexradArchiveKey.TryParse(
            "2026/05/04/KTLX/KTLX_20260504_120000_V06",
            NexradArchiveKey.BucketName,
            42,
            DateTimeOffset.Parse("2026-05-04T12:05:00Z"),
            out var file);

        Assert.True(parsed);
        Assert.NotNull(file);
        Assert.Equal("KTLX", file.RadarId);
        Assert.Equal("KTLX_20260504_120000_V06", file.FileName);
        Assert.Equal(42L, file.SizeBytes);
    }

    [Fact]
    public void VolumeTimestampParsesFromFileName()
    {
        var timestamp = NexradArchiveKey.TryParseVolumeTimestamp(
            "KTLX_20260504_120000_V06",
            new DateOnly(2026, 5, 4));

        Assert.Equal(new DateTimeOffset(2026, 5, 4, 12, 0, 0, TimeSpan.Zero), timestamp);
    }

    [Fact]
    public void VolumeTimestampParsesCompactFileName()
    {
        var timestamp = NexradArchiveKey.TryParseVolumeTimestamp(
            "KTLX20260504_120000_V06",
            new DateOnly(2026, 5, 4));

        Assert.Equal(new DateTimeOffset(2026, 5, 4, 12, 0, 0, TimeSpan.Zero), timestamp);
    }

    [Fact]
    public void VolumeTimestampIgnoresShortFileName()
    {
        var timestamp = NexradArchiveKey.TryParseVolumeTimestamp(
            "_20260504_1200",
            new DateOnly(2026, 5, 4));

        Assert.Null(timestamp);
    }
}
