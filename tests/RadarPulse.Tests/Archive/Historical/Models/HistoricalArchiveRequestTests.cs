using RadarPulse.Domain.Archive;

namespace RadarPulse.Tests.Archive;

public sealed class HistoricalArchiveRequestTests
{
    [Fact]
    public void ValidateForDiscoveryRequiresExplicitAllRadars()
    {
        var request = new HistoricalArchiveRequest(new DateOnly(2026, 5, 4));

        Assert.Throws<InvalidOperationException>(request.ValidateForDiscovery);
    }
}
