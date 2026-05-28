using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Presentation;

public sealed partial class RadarPulseCliRebalanceBenchmarkTests
{
    [Fact]
    public void ArchiveRebalanceBenchmarkCommandEmitsQueuedProviderTelemetry()
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
                "--queue-capacity",
                "2",
                "--queue-telemetry",
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
            Assert.Contains("Processing benchmark: rebalance-archive cache", result.StandardOutput);
            Assert.Contains("Provider mode: queued-owned", result.StandardOutput);
            Assert.Contains("Provider queue capacity: 2", result.StandardOutput);
            Assert.Contains("Provider overlap mode: none", result.StandardOutput);
            Assert.Contains("Retention strategy: snapshot-copy", result.StandardOutput);
            Assert.Contains("Provider queue retained byte capacity: none", result.StandardOutput);
            Assert.Contains("Provider mode source: explicit", result.StandardOutput);
            Assert.Contains("Provider overlap source: current-default", result.StandardOutput);
            Assert.Contains("Retention strategy source: current-default", result.StandardOutput);
            Assert.Contains("Provider queue capacity source: explicit", result.StandardOutput);
            Assert.Contains("Worker queue capacity source: not-applicable", result.StandardOutput);
            Assert.Contains("Provider queue retained byte capacity source: current-default", result.StandardOutput);
            Assert.Contains("Queue telemetry source: explicit", result.StandardOutput);
            Assert.Contains("Provider overlap telemetry source: not-applicable", result.StandardOutput);
            Assert.Contains("Provider overlap consumer delay source: not-applicable", result.StandardOutput);
            Assert.Contains("Execution mode source: current-default", result.StandardOutput);
            Assert.Contains("Worker count source: not-applicable", result.StandardOutput);
            Assert.Contains("Default-candidate contour: no", result.StandardOutput);
            Assert.Contains("Provider default rollout contour: no", result.StandardOutput);
            Assert.Contains("Provider rollout default expansion: no", result.StandardOutput);
            Assert.Contains("Provider fallback contour: no", result.StandardOutput);
            Assert.Contains("Provider overlap evidence contour: not-applicable", result.StandardOutput);
            Assert.Contains("Provider overlap evidence scope: not-applicable", result.StandardOutput);
            Assert.Contains(
                "Batch lifetime: leased batches are converted to owned snapshots before provider queue enqueue",
                result.StandardOutput);
            Assert.Contains("Provider queue telemetry: summary", result.StandardOutput);
            Assert.Contains("Provider queue owned snapshots: 0", result.StandardOutput);
            Assert.Contains("Provider queue owned snapshot events: 0", result.StandardOutput);
            Assert.Contains("Provider queue drain ms:", result.StandardOutput);
            Assert.Contains("Provider queue current pending retained batches: 0", result.StandardOutput);
            Assert.Contains("Provider queue current active retained batches: 0", result.StandardOutput);
            Assert.Contains("Provider queue current combined retained batches: 0", result.StandardOutput);
            Assert.Contains("Provider queue pending retained payload bytes high watermark: 0", result.StandardOutput);
            Assert.Contains("Provider queue active retained payload bytes high watermark: 0", result.StandardOutput);
            Assert.Contains("Provider queue combined retained payload bytes high watermark: 0", result.StandardOutput);
            Assert.Contains("Allocation attribution: summary", result.StandardOutput);
            Assert.Contains("Allocation measured counter scope: global", result.StandardOutput);
            Assert.Contains("Allocation processing callback counter scope: global", result.StandardOutput);
            Assert.Contains("Allocation measured bytes:", result.StandardOutput);
            Assert.Contains("Allocation processing callback bytes:", result.StandardOutput);
            Assert.Contains("Allocation replay and batch construction bytes:", result.StandardOutput);
            Assert.Contains("Allocation owned snapshot bytes: 0", result.StandardOutput);
            Assert.Contains("Allocation processing callback non-owned snapshot bytes:", result.StandardOutput);
            Assert.Contains("Allocation includes archive replay and batch construction: yes", result.StandardOutput);
            Assert.Contains("Allocation includes CLI formatting: no", result.StandardOutput);
            Assert.Contains("Retained payload telemetry: summary", result.StandardOutput);
            Assert.Contains("Retained payload allocation counter scope: current-thread", result.StandardOutput);
            Assert.Contains("Retained payload attempts: 0", result.StandardOutput);
            Assert.Contains("Retained payload allocated bytes: 0", result.StandardOutput);
            Assert.DoesNotContain("Provider overlap telemetry:", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

}
