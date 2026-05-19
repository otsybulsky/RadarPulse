using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Presentation;

public sealed class RadarPulseCliRebalanceBenchmarkTests
{
    [Fact]
    public void RebalanceBenchmarkOptionsParseWorkloadModeAndIterations()
    {
        var options = global::ProcessingBenchmarkRebalanceSyntheticOptions.Parse(
        [
            "--workload",
            "hot-shard",
            "--mode",
            "rebalance",
            "--validation-profile",
            "benchmark",
            "--quarantine-ttl-evaluations",
            "9",
            "--quarantine-sustained-cooling-samples",
            "7",
            "--quarantine-material-pressure-change",
            "0.5",
            "--execution",
            "async",
            "--workers",
            "2",
            "--queue-capacity",
            "1",
            "--iterations",
            "2",
            "--warmup-iterations",
            "0"
        ]);

        Assert.Equal(
            [RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard],
            options.Workloads);
        Assert.Equal(
            [RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession],
            options.Modes);
        Assert.Equal(RadarProcessingValidationProfile.Benchmark, options.ValidationProfile);
        var quarantineLifecycle = options.QuarantineLifecycleOverrides.ApplyTo(
            RadarProcessingQuarantineLifecycleOptions.Default);
        Assert.Equal(9, quarantineLifecycle.QuarantineTtlEvaluations);
        Assert.Equal(7, quarantineLifecycle.SustainedCoolingSampleCount);
        Assert.Equal(0.5, quarantineLifecycle.MaterialPressureChangeThreshold);
        Assert.Equal(2, options.Iterations);
        Assert.Equal(0, options.WarmupIterations);
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, options.ExecutionMode);
        Assert.NotNull(options.AsyncExecution);
        Assert.Equal(2, options.AsyncExecution.WorkerCount);
        Assert.Equal(1, options.AsyncExecution.QueueCapacity);
    }

    [Fact]
    public void RebalanceBenchmarkOptionsParseLifecycleWorkload()
    {
        var options = global::ProcessingBenchmarkRebalanceSyntheticOptions.Parse(
        [
            "--workload",
            "quarantine-successful-relief-clear",
            "--mode",
            "rebalance"
        ]);

        Assert.Equal(
            [RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSuccessfulReliefClear],
            options.Workloads);
        Assert.Equal(
            [RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession],
            options.Modes);
    }

    [Fact]
    public void RebalanceBenchmarkOptionsParseRetentionStressWorkload()
    {
        var options = global::ProcessingBenchmarkRebalanceSyntheticOptions.Parse(
        [
            "--workload",
            "counters-only-retention",
            "--mode",
            "rebalance"
        ]);

        Assert.Equal(
            [RadarProcessingSyntheticRebalanceWorkloadKind.CountersOnlyRetention],
            options.Workloads);
        Assert.Equal(
            [RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession],
            options.Modes);
    }

    [Fact]
    public void RebalanceBenchmarkOptionsRejectSequentialMode()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => global::ProcessingBenchmarkRebalanceSyntheticOptions.Parse(["--mode", "sequential"]));

        Assert.Contains("Unknown synthetic rebalance benchmark mode", exception.Message);

        var validationException = Assert.Throws<ArgumentException>(
            () => global::ProcessingBenchmarkRebalanceSyntheticOptions.Parse(
            [
                "--validation-profile",
                "verbose"
            ]));

        Assert.Contains("Unknown synthetic rebalance validation profile", validationException.Message);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => global::ProcessingBenchmarkRebalanceSyntheticOptions.Parse(
            [
                "--quarantine-ttl-evaluations",
                "0"
            ]));
    }

    [Fact]
    public void RebalanceBenchmarkOptionsRejectInvalidWorkerSettings()
    {
        var invalidWorkers = RunCli(
            "processing",
            "benchmark",
            "rebalance-synthetic",
            "--execution",
            "async",
            "--workers",
            "0");
        var invalidQueue = RunCli(
            "processing",
            "benchmark",
            "rebalance-synthetic",
            "--execution",
            "async",
            "--queue-capacity",
            "0");
        var nonAsyncWorkers = RunCli(
            "processing",
            "benchmark",
            "rebalance-synthetic",
            "--execution",
            "sync",
            "--workers",
            "2");

        Assert.Equal(1, invalidWorkers.ExitCode);
        Assert.Contains("--workers must be greater than zero.", invalidWorkers.StandardError);
        Assert.Equal(1, invalidQueue.ExitCode);
        Assert.Contains("--queue-capacity must be greater than zero.", invalidQueue.StandardError);
        Assert.Equal(1, nonAsyncWorkers.ExitCode);
        Assert.Contains("--workers and --queue-capacity require --execution async.", nonAsyncWorkers.StandardError);
    }

    [Fact]
    public void RebalanceBenchmarkCommandEmitsTopologyAndMoveCounters()
    {
        var result = RunCli(
            "processing",
            "benchmark",
            "rebalance-synthetic",
            "--workload",
            "hot-shard",
            "--mode",
            "rebalance",
            "--iterations",
            "1",
            "--warmup-iterations",
            "0");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Processing benchmark: rebalance-synthetic", result.StandardOutput);
        Assert.Contains("Workload: hot-shard", result.StandardOutput);
        Assert.Contains("Execution mode: partitioned", result.StandardOutput);
        Assert.Contains("Benchmark mode: rebalance-session", result.StandardOutput);
        Assert.Contains("Validation profile: diagnostic", result.StandardOutput);
        Assert.Contains("Telemetry retention mode: recent", result.StandardOutput);
        Assert.Contains("Allocation includes CLI formatting: no", result.StandardOutput);
        Assert.Contains("Topology versions per iteration:", result.StandardOutput);
        Assert.Contains("Accepted moves:", result.StandardOutput);
        Assert.Contains("Skipped reasons:", result.StandardOutput);
        Assert.Contains("Accepted move pressures:", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public void RebalanceBenchmarkCommandEmitsAsyncWorkerTelemetry()
    {
        var result = RunCli(
            "processing",
            "benchmark",
            "rebalance-synthetic",
            "--workload",
            "hot-shard",
            "--mode",
            "rebalance",
            "--execution",
            "async",
            "--workers",
            "2",
            "--queue-capacity",
            "1",
            "--iterations",
            "1",
            "--warmup-iterations",
            "0");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Processing benchmark: rebalance-synthetic", result.StandardOutput);
        Assert.Contains("Execution mode: async", result.StandardOutput);
        Assert.Contains("Worker count: 2", result.StandardOutput);
        Assert.Contains("Worker queue capacity: 1", result.StandardOutput);
        Assert.Contains("Worker completed batches: 2", result.StandardOutput);
        Assert.Contains("Benchmark mode: rebalance-session", result.StandardOutput);
        Assert.Contains("Accepted moves:", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public void RebalanceBenchmarkCommandAppliesValidationProfileWithoutReplacingWorkloadRetention()
    {
        var result = RunCli(
            "processing",
            "benchmark",
            "rebalance-synthetic",
            "--workload",
            "counters-only-retention",
            "--mode",
            "rebalance",
            "--validation-profile",
            "benchmark",
            "--iterations",
            "1",
            "--warmup-iterations",
            "0");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Workload: counters-only-retention", result.StandardOutput);
        Assert.Contains("Validation profile: benchmark", result.StandardOutput);
        Assert.Contains("Telemetry retention mode: counters", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public void RebalanceBenchmarkCommandAppliesPartialQuarantineLifecycleOverride()
    {
        var result = RunCli(
            "processing",
            "benchmark",
            "rebalance-synthetic",
            "--workload",
            "quarantine-ttl-retry",
            "--mode",
            "sampling",
            "--quarantine-sustained-cooling-samples",
            "7",
            "--iterations",
            "1",
            "--warmup-iterations",
            "0");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Workload: quarantine-ttl-retry", result.StandardOutput);
        Assert.Contains("Quarantine TTL evaluations: 1", result.StandardOutput);
        Assert.Contains("Quarantine sustained cooling samples: 7", result.StandardOutput);
        Assert.Contains("Quarantine material pressure change: 1.00", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public void ArchiveRebalanceBenchmarkOptionsParseFileModeAndTopology()
    {
        var options = global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
        [
            "--file",
            "data/nexrad/sample",
            "--mode",
            "sampling",
            "--partitions",
            "24",
            "--shards",
            "4",
            "--iterations",
            "2",
            "--validation-profile",
            "essential",
            "--quarantine-ttl-evaluations",
            "12",
            "--quarantine-sustained-cooling-samples",
            "5",
            "--quarantine-material-pressure-change",
            "0.75",
            "--warmup-iterations",
            "1",
            "--parallelism",
            "2",
            "--execution",
            "async",
            "--workers",
            "3",
            "--queue-capacity",
            "2"
        ]);

        Assert.Equal("data/nexrad/sample", options.FilePath);
        Assert.Equal(
            [RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly],
            options.Modes);
        Assert.Equal(24, options.PartitionCount);
        Assert.Equal(4, options.ShardCount);
        Assert.Equal(2, options.Iterations);
        Assert.Equal(1, options.WarmupIterations);
        Assert.Equal(2, options.Parallelism);
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, options.ExecutionMode);
        Assert.NotNull(options.AsyncExecution);
        Assert.Equal(3, options.AsyncExecution.WorkerCount);
        Assert.Equal(2, options.AsyncExecution.QueueCapacity);
        Assert.Equal(RadarProcessingArchiveProviderMode.BlockingBorrowed, options.ProviderMode);
        Assert.Equal(1, options.ProviderQueueCapacity);
        Assert.Null(options.ProviderQueueTimeout);
        Assert.Equal(ProcessingBenchmarkProviderQueueTelemetryOutput.Summary, options.QueueTelemetryOutput);
        Assert.Equal(RadarProcessingValidationProfile.Essential, options.ValidationProfile);
        var quarantineLifecycle = options.QuarantineLifecycleOverrides.ApplyTo(
            RadarProcessingQuarantineLifecycleOptions.Default);
        Assert.Equal(12, quarantineLifecycle.QuarantineTtlEvaluations);
        Assert.Equal(5, quarantineLifecycle.SustainedCoolingSampleCount);
        Assert.Equal(0.75, quarantineLifecycle.MaterialPressureChangeThreshold);
        Assert.Equal(RadarProcessingDiagnosticRetentionMode.Recent, options.TelemetryRetention.RetentionMode);
    }

    [Fact]
    public void ArchiveRebalanceBenchmarkOptionsParseQueuedProviderSettings()
    {
        var options = global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
        [
            "--cache",
            "data/nexrad",
            "--provider",
            "queued-owned",
            "--queue-capacity",
            "3",
            "--queue-timeout-ms",
            "250",
            "--queue-telemetry",
            "recent",
            "--mode",
            "static"
        ]);

        Assert.Equal(RadarProcessingArchiveProviderMode.QueuedOwned, options.ProviderMode);
        Assert.Equal(3, options.ProviderQueueCapacity);
        Assert.Equal(TimeSpan.FromMilliseconds(250), options.ProviderQueueTimeout);
        Assert.Equal(ProcessingBenchmarkProviderQueueTelemetryOutput.Recent, options.QueueTelemetryOutput);
        Assert.Equal(RadarProcessingExecutionMode.PartitionedBarrier, options.ExecutionMode);
        Assert.Null(options.AsyncExecution);
    }

    [Fact]
    public void ArchiveRebalanceBenchmarkOptionsParseCacheSelection()
    {
        var options = global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
        [
            "--cache",
            "data/nexrad",
            "--date",
            "2026-05-04",
            "--radar",
            "ktlx",
            "--max-files",
            "100",
            "--mode",
            "rebalance"
        ]);

        Assert.Null(options.FilePath);
        Assert.Equal("data/nexrad", options.CachePath);
        Assert.Equal(new DateOnly(2026, 5, 4), options.Date);
        Assert.Equal("KTLX", options.RadarId);
        Assert.Equal(100, options.MaxFiles);
        Assert.Equal(
            [RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession],
            options.Modes);
    }

    [Fact]
    public void ArchiveRebalanceBenchmarkOptionsParseRetentionSettings()
    {
        var options = global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
        [
            "--cache",
            "data/nexrad",
            "--mode",
            "rebalance",
            "--retention-mode",
            "counters",
            "--max-retained-decisions",
            "4",
            "--max-retained-transitions",
            "3",
            "--max-retained-accepted-moves",
            "2",
            "--max-retained-validation-failures",
            "1"
        ]);

        Assert.Equal(
            RadarProcessingDiagnosticRetentionMode.Counters,
            options.TelemetryRetention.RetentionMode);
        Assert.Equal(4, options.TelemetryRetention.MaxRetainedDecisions);
        Assert.Equal(3, options.TelemetryRetention.MaxRetainedLifecycleTransitions);
        Assert.Equal(2, options.TelemetryRetention.MaxRetainedAcceptedMoves);
        Assert.Equal(1, options.TelemetryRetention.MaxRetainedValidationFailures);
    }

    [Fact]
    public void ArchiveRebalanceBenchmarkOptionsParsePressureSkewSettings()
    {
        var options = global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
        [
            "--cache",
            "data/nexrad",
            "--mode",
            "rebalance",
            "--skew-profile",
            "rotating-hot-shard",
            "--skew-factor",
            "2.5",
            "--skew-period",
            "4"
        ]);

        Assert.Equal(RadarProcessingPressureSkewProfile.RotatingHotShard, options.PressureSkew.Profile);
        Assert.Equal(2.5, options.PressureSkew.Factor);
        Assert.Equal(4, options.PressureSkew.Period);
    }

    [Fact]
    public void ArchiveRebalanceBenchmarkOptionsRequireFileAndCompatibleTopology()
    {
        Assert.Throws<InvalidOperationException>(
            () => global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(["--mode", "static"]));
        Assert.Throws<InvalidOperationException>(
            () => global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
            [
                "--file",
                "data/nexrad/sample",
                "--cache",
                "data/nexrad"
            ]));
        Assert.Throws<InvalidOperationException>(
            () => global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
            [
                "--file",
                "data/nexrad/sample",
                "--partitions",
                "2",
                "--shards",
                "4"
            ]));
        Assert.Throws<ArgumentException>(
            () => global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
            [
                "--cache",
                "data/nexrad",
                "--retention-mode",
                "forever"
            ]));
        Assert.Throws<ArgumentException>(
            () => global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
            [
                "--cache",
                "data/nexrad",
                "--validation-profile",
                "verbose"
            ]));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
            [
                "--cache",
                "data/nexrad",
                "--quarantine-material-pressure-change",
                "-0.1"
            ]));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
            [
                "--cache",
                "data/nexrad",
                "--max-retained-decisions",
                "-1"
            ]));
        Assert.Throws<ArgumentException>(
            () => global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
            [
                "--cache",
                "data/nexrad",
                "--skew-profile",
                "random"
            ]));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
            [
                "--cache",
                "data/nexrad",
                "--skew-period",
                "0"
            ]));
        Assert.Throws<ArgumentException>(
            () => global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
            [
                "--cache",
                "data/nexrad",
                "--provider",
                "leased"
            ]));
        Assert.Throws<ArgumentException>(
            () => global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
            [
                "--cache",
                "data/nexrad",
                "--queue-telemetry",
                "verbose"
            ]));
        Assert.Throws<InvalidOperationException>(
            () => global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
            [
                "--cache",
                "data/nexrad",
                "--queue-capacity",
                "2"
            ]));
        Assert.Throws<InvalidOperationException>(
            () => global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
            [
                "--cache",
                "data/nexrad",
                "--provider",
                "queued-owned",
                "--queue-timeout-ms",
                "0"
            ]));
        Assert.Throws<InvalidOperationException>(
            () => global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
            [
                "--cache",
                "data/nexrad",
                "--queue-timeout-ms",
                "10"
            ]));
        Assert.Throws<ArgumentException>(
            () => global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
            [
                "--cache",
                "data/nexrad",
                "--execution",
                "parallel"
            ]));
        Assert.Throws<InvalidOperationException>(
            () => global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
            [
                "--cache",
                "data/nexrad",
                "--execution",
                "async",
                "--workers",
                "0"
            ]));
        Assert.Throws<InvalidOperationException>(
            () => global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
            [
                "--cache",
                "data/nexrad",
                "--execution",
                "async",
                "--queue-capacity",
                "0"
            ]));
        Assert.Throws<InvalidOperationException>(
            () => global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
            [
                "--cache",
                "data/nexrad",
                "--execution",
                "sync",
                "--workers",
                "2"
            ]));
    }

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
            Assert.Contains(
                "Batch lifetime: leased batches are converted to owned snapshots before provider queue enqueue",
                result.StandardOutput);
            Assert.Contains("Provider queue telemetry: summary", result.StandardOutput);
            Assert.Contains("Provider queue owned snapshots: 0", result.StandardOutput);
            Assert.Contains("Provider queue drain ms:", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static CliResult RunCli(params string[] args)
    {
        var assemblyPath = typeof(global::ProcessingBenchmarkRebalanceSyntheticOptions).Assembly.Location;
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add(assemblyPath);
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo) ??
                            throw new InvalidOperationException("Failed to start RadarPulse CLI.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(milliseconds: 30_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("RadarPulse CLI smoke test timed out.");
        }

        return new CliResult(
            process.ExitCode,
            standardOutput.GetAwaiter().GetResult(),
            standardError.GetAwaiter().GetResult());
    }

    private sealed record CliResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
