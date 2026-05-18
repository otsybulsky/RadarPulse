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
        Assert.Equal(RadarProcessingValidationProfile.Essential, options.ValidationProfile);
        Assert.Equal(RadarProcessingDiagnosticRetentionMode.Recent, options.TelemetryRetention.RetentionMode);
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
