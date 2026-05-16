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
        Assert.Contains("Topology versions per iteration:", result.StandardOutput);
        Assert.Contains("Accepted moves:", result.StandardOutput);
        Assert.Contains("Skipped reasons:", result.StandardOutput);
        Assert.Contains("Accepted move pressures:", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
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
