using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Presentation;

public sealed partial class RadarPulseCliRebalanceBenchmarkTests
{
    [Fact]
    public void ArchiveRebalanceBenchmarkCommandEmitsOverlapTelemetry()
    {
        var directory = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "notes.txt"), [1, 2, 3]);

        try
        {
            var result = RunCli(
                "processing",
                "benchmark",
                "rebalance-archive",
                "--cache",
                directory,
                "--max-files",
                "1",
                "--mode",
                "static",
                "--provider",
                "queued-owned",
                "--provider-overlap",
                "producer-consumer",
                "--retention-strategy",
                "pooled-copy",
                "--queue-retained-bytes",
                "4096",
                "--overlap-consumer-delay-ms",
                "1",
                "--overlap-telemetry",
                "summary",
                "--partitions",
                "4",
                "--shards",
                "2",
                "--iterations",
                "1",
                "--warmup-iterations",
                "0");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Provider overlap mode: producer-consumer", result.StandardOutput);
            Assert.Contains("Provider overlap consumer delay ms: 1.00", result.StandardOutput);
            Assert.Contains("Retention strategy: pooled-copy", result.StandardOutput);
            Assert.Contains("Provider queue retained byte capacity: 4_096", result.StandardOutput);
            Assert.Contains("Provider mode source: explicit", result.StandardOutput);
            Assert.Contains("Provider overlap source: explicit", result.StandardOutput);
            Assert.Contains("Retention strategy source: explicit", result.StandardOutput);
            Assert.Contains("Provider queue capacity source: current-default", result.StandardOutput);
            Assert.Contains("Worker queue capacity source: not-applicable", result.StandardOutput);
            Assert.Contains("Provider queue retained byte capacity source: explicit", result.StandardOutput);
            Assert.Contains("Queue telemetry source: current-default", result.StandardOutput);
            Assert.Contains("Provider overlap telemetry source: explicit", result.StandardOutput);
            Assert.Contains("Provider overlap consumer delay source: explicit", result.StandardOutput);
            Assert.Contains("Execution mode source: current-default", result.StandardOutput);
            Assert.Contains("Worker count source: not-applicable", result.StandardOutput);
            Assert.Contains("Default-candidate contour: no", result.StandardOutput);
            Assert.Contains("Provider default rollout contour: no", result.StandardOutput);
            Assert.Contains("Provider rollout default expansion: no", result.StandardOutput);
            Assert.Contains("Provider fallback contour: no", result.StandardOutput);
            Assert.Contains("Provider overlap evidence contour: controlled-proof", result.StandardOutput);
            Assert.Contains("Provider overlap evidence scope: controlled-mechanics-proof", result.StandardOutput);
            Assert.Contains("Retained payload strategy: pooled-copy", result.StandardOutput);
            Assert.Contains("Provider overlap telemetry: summary", result.StandardOutput);
            Assert.Contains("Provider overlap retained payload strategy: pooled-copy", result.StandardOutput);
            Assert.Contains("Provider overlap retained payload bytes high watermark: 0", result.StandardOutput);
            Assert.Contains("Provider overlap current pending retained batches: 0", result.StandardOutput);
            Assert.Contains("Provider overlap current active retained batches: 0", result.StandardOutput);
            Assert.Contains("Provider overlap current combined retained batches: 0", result.StandardOutput);
            Assert.Contains("Provider overlap pending retained payload bytes high watermark: 0", result.StandardOutput);
            Assert.Contains("Provider overlap active retained payload bytes high watermark: 0", result.StandardOutput);
            Assert.Contains("Provider overlap combined retained payload bytes high watermark: 0", result.StandardOutput);
            Assert.Contains("Provider overlap retention allocated bytes: 0", result.StandardOutput);
            Assert.Contains("Provider overlap measured allocation counter scope: global", result.StandardOutput);
            Assert.Contains("Provider overlap measured allocated bytes:", result.StandardOutput);
            Assert.Contains("Provider overlap unattributed allocated bytes:", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

}
