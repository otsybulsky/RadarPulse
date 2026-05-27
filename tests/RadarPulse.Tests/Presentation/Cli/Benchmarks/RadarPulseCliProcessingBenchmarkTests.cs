using System.Diagnostics;
using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Presentation;

public sealed class RadarPulseCliProcessingBenchmarkTests
{
    [Fact]
    public void SyntheticBenchmarkOptionsRejectInvalidWorkerSettings()
    {
        var invalidWorkers = RunCli(
            "processing",
            "benchmark",
            "synthetic",
            "--mode",
            "async",
            "--workers",
            "0");
        var invalidQueue = RunCli(
            "processing",
            "benchmark",
            "synthetic",
            "--mode",
            "async",
            "--queue-capacity",
            "0");
        var nonAsyncWorkers = RunCli(
            "processing",
            "benchmark",
            "synthetic",
            "--mode",
            "partitioned",
            "--workers",
            "2");

        Assert.Equal(1, invalidWorkers.ExitCode);
        Assert.Contains("--workers must be greater than zero.", invalidWorkers.StandardError);
        Assert.Equal(1, invalidQueue.ExitCode);
        Assert.Contains("--queue-capacity must be greater than zero.", invalidQueue.StandardError);
        Assert.Equal(1, nonAsyncWorkers.ExitCode);
        Assert.Contains("--workers and --queue-capacity require --mode async.", nonAsyncWorkers.StandardError);
    }

    [Fact]
    public void SyntheticBenchmarkCommandEmitsAsyncWorkerTelemetry()
    {
        var result = RunCli(
            "processing",
            "benchmark",
            "synthetic",
            "--mode",
            "async",
            "--sources",
            "6",
            "--batches",
            "1",
            "--events-per-batch",
            "6",
            "--payload-values",
            "1",
            "--partitions",
            "6",
            "--shards",
            "3",
            "--workers",
            "3",
            "--queue-capacity",
            "1",
            "--iterations",
            "1",
            "--warmup-iterations",
            "0");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Processing benchmark: synthetic", result.StandardOutput);
        Assert.Contains("Execution mode: async", result.StandardOutput);
        Assert.Contains("Validation profile: benchmark", result.StandardOutput);
        Assert.Contains("Worker count: 3", result.StandardOutput);
        Assert.Contains("Worker queue capacity: 1", result.StandardOutput);
        Assert.Contains("Worker completed batches: 1", result.StandardOutput);
        Assert.Contains("Async validation: yes", result.StandardOutput);
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
