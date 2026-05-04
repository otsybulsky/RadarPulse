using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed class S3NexradArchiveClientIntegrationTests
{
    [IntegrationFact]
    public async Task BuildManifestAsyncListsPublicS3Archive()
    {
        using var httpClient = new HttpClient();
        var client = new S3NexradArchiveClient(httpClient);
        var request = new HistoricalArchiveRequest(
            new DateOnly(2026, 5, 4),
            RadarIds: ["KTLX"],
            MaxFiles: 1);

        var manifest = await client.BuildManifestAsync(request, CancellationToken.None);

        var file = Assert.Single(manifest.Files);
        Assert.Equal("KTLX", file.RadarId);
        Assert.True(file.SizeBytes > 0);
    }
}
