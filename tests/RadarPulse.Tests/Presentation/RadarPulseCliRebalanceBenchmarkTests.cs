using System.Diagnostics;
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
        Assert.Equal(2, options.Iterations);
        Assert.Equal(0, options.WarmupIterations);
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
    public void RebalanceBenchmarkOptionsRejectSequentialMode()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => global::ProcessingBenchmarkRebalanceSyntheticOptions.Parse(["--mode", "sequential"]));

        Assert.Contains("Unknown synthetic rebalance benchmark mode", exception.Message);
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
            "--warmup-iterations",
            "1",
            "--parallelism",
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
