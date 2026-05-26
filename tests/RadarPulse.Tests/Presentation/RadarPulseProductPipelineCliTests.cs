using System.Diagnostics;

namespace RadarPulse.Tests.Presentation;

public sealed class RadarPulseProductPipelineCliTests
{
    [Fact]
    public void UsageListsProductPipelineCommands()
    {
        var result = RunCli();

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("radarpulse product pipeline demo", result.StandardOutput);
        Assert.Contains("radarpulse product pipeline run-archive", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public void ProductPipelineDemoCommandPrintsCompletedRunSummary()
    {
        var result = RunCli(
            "product",
            "pipeline",
            "demo",
            "--run-id",
            "cli-product-demo",
            "--sources",
            "2",
            "--batches",
            "2",
            "--events-per-batch",
            "2");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Product pipeline: demo", result.StandardOutput);
        Assert.Contains("Run id: cli-product-demo", result.StandardOutput);
        Assert.Contains("Input kind: synthetic", result.StandardOutput);
        Assert.Contains("Run state: completed", result.StandardOutput);
        Assert.Contains("Readiness: ready", result.StandardOutput);
        Assert.Contains("Handler mode: handler-free", result.StandardOutput);
        Assert.Contains("Read model: published", result.StandardOutput);
        Assert.Contains("Accepted batches: 2", result.StandardOutput);
        Assert.Contains("Processing completeness: succeeded", result.StandardOutput);
        Assert.Contains("Configuration contour:", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public void ProductPipelineDemoCommandPrintsMergeableHandlerPosture()
    {
        var result = RunCli(
            "product",
            "pipeline",
            "demo",
            "--run-id",
            "cli-product-handler",
            "--handlers",
            "counter-checksum");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Run id: cli-product-handler", result.StandardOutput);
        Assert.Contains("Handler mode: mergeable-delta", result.StandardOutput);
        Assert.Contains("Readiness: ready", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public void InvalidProductPipelineCommandReturnsValidationError()
    {
        var result = RunCli(
            "product",
            "pipeline",
            "demo",
            "--sources",
            "0");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("--sources must be greater than zero.", result.StandardError);
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
