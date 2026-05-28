using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Presentation;

public sealed partial class RadarPulseCliRebalanceBenchmarkTests
{
    [Fact]
    public void OrderedArchiveProcessingOptionsParseCacheAndActiveBatchCapacity()
    {
        var options = global::ProcessingBenchmarkOrderedArchiveProcessingOptions.Parse(
        [
            "--cache",
            "data/nexrad",
            "--max-files",
            "1000000",
            "--date",
            "2026-05-04",
            "--radar",
            "ktlx",
            "--partitions",
            "24",
            "--shards",
            "4",
            "--active-batches",
            "3",
            "--iterations",
            "2",
            "--warmup-iterations",
            "1",
            "--parallelism",
            "8",
            "--handlers",
            "counter-checksum-heavy",
            "--queue-telemetry",
            "recent",
            "--overlap-telemetry",
            "none"
        ]);

        Assert.Null(options.FilePath);
        Assert.Equal("data/nexrad", options.CachePath);
        Assert.Equal(new DateOnly(2026, 5, 4), options.Date);
        Assert.Equal("KTLX", options.RadarId);
        Assert.Equal(1_000_000, options.MaxFiles);
        Assert.Equal(24, options.PartitionCount);
        Assert.Equal(4, options.ShardCount);
        Assert.Equal(3, options.ActiveBatchCapacity);
        Assert.Equal(2, options.Iterations);
        Assert.Equal(1, options.WarmupIterations);
        Assert.Equal(8, options.Parallelism);
        Assert.Equal(RadarProcessingBenchmarkHandlerSet.CounterChecksumHeavy, options.HandlerSet);
        Assert.Equal(ProcessingBenchmarkProviderQueueTelemetryOutput.Recent, options.QueueTelemetryOutput);
        Assert.Equal(ProcessingBenchmarkProviderOverlapTelemetryOutput.None, options.OverlapTelemetryOutput);
    }

    [Fact]
    public void OrderedArchiveProcessingCommandUsesRunProcessingAsyncRuntimeBaseline()
    {
        var directory = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "notes.txt"), [1, 2, 3]);

        try
        {
            var result = RunCli(
                "processing",
                "benchmark",
                "ordered-archive-processing",
                "--cache",
                directory,
                "--max-files",
                "1",
                "--partitions",
                "4",
                "--shards",
                "2",
                "--active-batches",
                "2",
                "--iterations",
                "1",
                "--warmup-iterations",
                "0",
                "--queue-telemetry",
                "none",
                "--overlap-telemetry",
                "none");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Processing benchmark: ordered-archive-processing cache", result.StandardOutput);
            Assert.Contains("Measured contour: Archive replay to RadarEventBatch through runtime/archive MVP processing path", result.StandardOutput);
            Assert.Contains("Processing path: RunProcessingAsync ordered active-batch drain", result.StandardOutput);
            Assert.Contains("Provider mode: queued-owned", result.StandardOutput);
            Assert.Contains("Handler set: none", result.StandardOutput);
            Assert.Contains("Provider overlap mode: producer-consumer", result.StandardOutput);
            Assert.Contains("Retention strategy: pooled-copy", result.StandardOutput);
            Assert.Contains("Provider mode source: runtime-archive-baseline", result.StandardOutput);
            Assert.Contains("Execution mode source: runtime-archive-baseline", result.StandardOutput);
            Assert.Contains("Ordered active batch capacity: 2", result.StandardOutput);
            Assert.Contains("Retained payload prewarm: yes", result.StandardOutput);
            Assert.Contains("Examined files per iteration: 1", result.StandardOutput);
            Assert.Contains("Skipped files per iteration: 1", result.StandardOutput);
            Assert.Contains("Published files per iteration: 0", result.StandardOutput);
            Assert.Contains("Run status: completed", result.StandardOutput);
            Assert.Contains("Consumer status: completed", result.StandardOutput);
            Assert.Contains("Processing completeness: succeeded", result.StandardOutput);
            Assert.Contains("Processing validation failed batches: 0", result.StandardOutput);
            Assert.Contains("Final processed batches: 0", result.StandardOutput);
            Assert.Contains("End-to-end allocated bytes:", result.StandardOutput);
            Assert.DoesNotContain("Provider queue telemetry:", result.StandardOutput);
            Assert.DoesNotContain("Provider overlap telemetry:", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void OrderedArchiveProcessingCommandAcceptsHeavyHandlerSet()
    {
        var directory = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "notes.txt"), [1, 2, 3]);

        try
        {
            var result = RunCli(
                "processing",
                "benchmark",
                "ordered-archive-processing",
                "--cache",
                directory,
                "--max-files",
                "1",
                "--partitions",
                "4",
                "--shards",
                "2",
                "--active-batches",
                "2",
                "--handlers",
                "counter-checksum-heavy",
                "--iterations",
                "1",
                "--warmup-iterations",
                "0",
                "--queue-telemetry",
                "none",
                "--overlap-telemetry",
                "none");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Processing benchmark: ordered-archive-processing cache", result.StandardOutput);
            Assert.Contains("Processing path: RunMvpProcessingAsync handler delta/merge", result.StandardOutput);
            Assert.Contains("Handler set: counter-checksum-heavy", result.StandardOutput);
            Assert.Contains("Processing completeness: succeeded", result.StandardOutput);
            Assert.Contains("Final processed batches: 0", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

}
