using System.Buffers.Binary;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class ArchiveTwoMessageStreamScannerTests
{
    [Fact]
    public void IgnoresShortNonMessageTail()
    {
        var builder = new ArchiveTwoMessageSummaryBuilder();
        var scanner = new ArchiveTwoMessageStreamScanner(builder);

        scanner.Append([0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17]);
        scanner.Complete();

        Assert.Equal(0, builder.Build().MessageCount);
    }
}
