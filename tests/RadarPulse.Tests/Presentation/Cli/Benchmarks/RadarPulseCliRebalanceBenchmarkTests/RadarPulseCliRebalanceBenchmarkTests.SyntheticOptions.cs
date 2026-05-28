using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Presentation;

public sealed partial class RadarPulseCliRebalanceBenchmarkTests
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
            "--active-batches",
            "3",
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
        Assert.Equal(3, options.OrderedActiveBatchCapacity);
    }

    [Fact]
    public void RebalanceBenchmarkOptionsParseOrderedRebalanceMode()
    {
        var options = global::ProcessingBenchmarkRebalanceSyntheticOptions.Parse(
        [
            "--mode",
            "ordered-rebalance",
            "--active-batches",
            "4"
        ]);

        Assert.Equal(
            [RadarProcessingSyntheticRebalanceBenchmarkMode.OrderedRebalanceSession],
            options.Modes);
        Assert.Equal(4, options.OrderedActiveBatchCapacity);
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

        var activeBatchException = Assert.Throws<InvalidOperationException>(
            () => global::ProcessingBenchmarkRebalanceSyntheticOptions.Parse(
            [
                "--active-batches",
                "0"
            ]));

        Assert.Contains("--active-batches must be greater than zero", activeBatchException.Message);
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

}
