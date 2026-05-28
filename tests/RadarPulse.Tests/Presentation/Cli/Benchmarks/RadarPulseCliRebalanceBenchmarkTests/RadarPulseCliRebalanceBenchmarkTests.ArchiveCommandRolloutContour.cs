using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Presentation;

public sealed partial class RadarPulseCliRebalanceBenchmarkTests
{
    [Fact]
    public void ArchiveRebalanceBenchmarkCommandLabelsDefaultCandidateContour()
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
                "--execution",
                "async",
                "--workers",
                "4",
                "--queue-capacity",
                "8",
                "--queue-retained-bytes",
                "536870912",
                "--queue-telemetry",
                "summary",
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
            Assert.Contains("Provider mode: queued-owned", result.StandardOutput);
            Assert.Contains("Provider queue capacity: 8", result.StandardOutput);
            Assert.Contains("Provider overlap mode: producer-consumer", result.StandardOutput);
            Assert.Contains("Provider overlap consumer delay ms: 0.00", result.StandardOutput);
            Assert.Contains("Retention strategy: pooled-copy", result.StandardOutput);
            Assert.Contains("Provider queue retained byte capacity: 536_870_912", result.StandardOutput);
            Assert.Contains("Retained payload prewarm: yes", result.StandardOutput);
            Assert.Contains("Retained payload prewarm event count: 65_536", result.StandardOutput);
            Assert.Contains("Retained payload prewarm payload bytes: 67_108_864", result.StandardOutput);
            Assert.Contains("Retained payload prewarm batch count: 1", result.StandardOutput);
            Assert.Contains("Retained payload prewarm allocated bytes:", result.StandardOutput);
            Assert.Contains("Provider mode source: explicit", result.StandardOutput);
            Assert.Contains("Provider overlap source: explicit", result.StandardOutput);
            Assert.Contains("Retention strategy source: explicit", result.StandardOutput);
            Assert.Contains("Provider queue capacity source: explicit", result.StandardOutput);
            Assert.Contains("Worker queue capacity source: explicit", result.StandardOutput);
            Assert.Contains("Provider queue retained byte capacity source: explicit", result.StandardOutput);
            Assert.Contains("Queue telemetry source: explicit", result.StandardOutput);
            Assert.Contains("Provider overlap telemetry source: explicit", result.StandardOutput);
            Assert.Contains("Provider overlap consumer delay source: current-default", result.StandardOutput);
            Assert.Contains("Execution mode source: explicit", result.StandardOutput);
            Assert.Contains("Worker count source: explicit", result.StandardOutput);
            Assert.Contains("Default-candidate contour: yes", result.StandardOutput);
            Assert.Contains("Provider default rollout contour: yes", result.StandardOutput);
            Assert.Contains("Provider rollout default expansion: no", result.StandardOutput);
            Assert.Contains("Provider fallback contour: no", result.StandardOutput);
            Assert.Contains("Provider overlap evidence contour: natural-default-candidate", result.StandardOutput);
            Assert.Contains("Provider overlap evidence scope: natural-readiness", result.StandardOutput);
            Assert.Contains("Provider queue combined retained payload bytes high watermark: 0", result.StandardOutput);
            Assert.Contains("Provider overlap combined retained payload bytes high watermark: 0", result.StandardOutput);
            Assert.Contains("Execution mode: async", result.StandardOutput);
            Assert.Contains("Processing completeness: succeeded", result.StandardOutput);
            Assert.Contains("Processing validation failed batches: 0", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ArchiveRebalanceBenchmarkCommandUsesRolloutDefaultsWhenProviderOmitted()
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
            Assert.Contains("Provider queue capacity: 8", result.StandardOutput);
            Assert.Contains("Provider overlap mode: producer-consumer", result.StandardOutput);
            Assert.Contains("Provider overlap consumer delay ms: 0.00", result.StandardOutput);
            Assert.Contains("Retention strategy: pooled-copy", result.StandardOutput);
            Assert.Contains("Provider queue retained byte capacity: 536_870_912", result.StandardOutput);
            Assert.Contains("Retained payload prewarm: yes", result.StandardOutput);
            Assert.Contains("Retained payload prewarm event count: 65_536", result.StandardOutput);
            Assert.Contains("Retained payload prewarm payload bytes: 67_108_864", result.StandardOutput);
            Assert.Contains("Retained payload prewarm batch count: 1", result.StandardOutput);
            Assert.Contains("Retained payload prewarm allocated bytes:", result.StandardOutput);
            Assert.Contains(
                "Batch lifetime: leased batches are converted to owned snapshots before provider queue enqueue",
                result.StandardOutput);
            Assert.Contains("Provider mode source: rollout-default", result.StandardOutput);
            Assert.Contains("Provider overlap source: rollout-default", result.StandardOutput);
            Assert.Contains("Retention strategy source: rollout-default", result.StandardOutput);
            Assert.Contains("Provider queue capacity source: rollout-default", result.StandardOutput);
            Assert.Contains("Worker queue capacity source: rollout-default", result.StandardOutput);
            Assert.Contains("Provider queue retained byte capacity source: rollout-default", result.StandardOutput);
            Assert.Contains("Queue telemetry source: rollout-default", result.StandardOutput);
            Assert.Contains("Provider overlap telemetry source: rollout-default", result.StandardOutput);
            Assert.Contains("Provider overlap consumer delay source: rollout-default", result.StandardOutput);
            Assert.Contains("Execution mode source: rollout-default", result.StandardOutput);
            Assert.Contains("Worker count source: rollout-default", result.StandardOutput);
            Assert.Contains("Default-candidate contour: yes", result.StandardOutput);
            Assert.Contains("Provider default rollout contour: yes", result.StandardOutput);
            Assert.Contains("Provider rollout default expansion: yes", result.StandardOutput);
            Assert.Contains("Provider fallback contour: no", result.StandardOutput);
            Assert.Contains("Provider overlap evidence contour: natural-default-candidate", result.StandardOutput);
            Assert.Contains("Provider overlap evidence scope: natural-readiness", result.StandardOutput);
            Assert.Contains("Execution mode: async", result.StandardOutput);
            Assert.Contains("Processing completeness: succeeded", result.StandardOutput);
            Assert.Contains("Processing validation failed batches: 0", result.StandardOutput);
            Assert.Contains("Provider queue telemetry: summary", result.StandardOutput);
            Assert.Contains("Retained payload telemetry: summary", result.StandardOutput);
            Assert.Contains("Retained payload allocation counter scope: current-thread", result.StandardOutput);
            Assert.Contains("Retained payload allocated bytes: 0", result.StandardOutput);
            Assert.Contains("Retained event array pool rents: 0", result.StandardOutput);
            Assert.Contains("Retained event array pool returns: 0", result.StandardOutput);
            Assert.Contains("Retained event array pool misses: 0", result.StandardOutput);
            Assert.Contains("Retained byte array pool rents: 0", result.StandardOutput);
            Assert.Contains("Retained byte array pool returns: 0", result.StandardOutput);
            Assert.Contains("Retained byte array pool misses: 0", result.StandardOutput);
            Assert.Contains("Provider overlap telemetry: summary", result.StandardOutput);
            Assert.Contains("Provider overlap retained payload strategy: pooled-copy", result.StandardOutput);
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
