using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Presentation;

public sealed partial class RadarPulseCliRebalanceBenchmarkTests
{
    [Fact]
    public void ArchiveRebalanceBenchmarkCommandSuppressesOptionalTelemetryWhenNone()
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
                "--queue-telemetry",
                "none",
                "--overlap-telemetry",
                "none",
                "--partitions",
                "4",
                "--shards",
                "2",
                "--iterations",
                "1",
                "--warmup-iterations",
                "0");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Provider mode: queued-owned", result.StandardOutput);
            Assert.Contains("Provider overlap mode: producer-consumer", result.StandardOutput);
            Assert.Contains("Provider mode source: explicit", result.StandardOutput);
            Assert.Contains("Provider overlap source: explicit", result.StandardOutput);
            Assert.Contains("Retention strategy source: explicit", result.StandardOutput);
            Assert.Contains("Provider queue capacity source: current-default", result.StandardOutput);
            Assert.Contains("Worker queue capacity source: not-applicable", result.StandardOutput);
            Assert.Contains("Provider queue retained byte capacity source: current-default", result.StandardOutput);
            Assert.Contains("Queue telemetry source: explicit", result.StandardOutput);
            Assert.Contains("Provider overlap telemetry source: explicit", result.StandardOutput);
            Assert.Contains("Provider overlap consumer delay source: current-default", result.StandardOutput);
            Assert.Contains("Execution mode source: current-default", result.StandardOutput);
            Assert.Contains("Worker count source: not-applicable", result.StandardOutput);
            Assert.Contains("Default-candidate contour: no", result.StandardOutput);
            Assert.Contains("Provider default rollout contour: no", result.StandardOutput);
            Assert.Contains("Provider rollout default expansion: no", result.StandardOutput);
            Assert.Contains("Provider fallback contour: no", result.StandardOutput);
            Assert.Contains("Provider overlap evidence contour: natural-opt-in", result.StandardOutput);
            Assert.Contains("Provider overlap evidence scope: opt-in-diagnostic", result.StandardOutput);
            Assert.Contains("Retained payload telemetry: summary", result.StandardOutput);
            Assert.DoesNotContain("Provider queue telemetry:", result.StandardOutput);
            Assert.DoesNotContain("Provider queue current pending retained batches:", result.StandardOutput);
            Assert.DoesNotContain("Provider queue active retained payload bytes high watermark:", result.StandardOutput);
            Assert.DoesNotContain("Provider overlap telemetry:", result.StandardOutput);
            Assert.DoesNotContain("Provider overlap current pending retained batches:", result.StandardOutput);
            Assert.DoesNotContain("Provider overlap active retained payload bytes high watermark:", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

}
