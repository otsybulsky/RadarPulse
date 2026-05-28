using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Presentation;

public sealed partial class RadarPulseCliRebalanceBenchmarkTests
{
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

}
