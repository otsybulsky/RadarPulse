using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Presentation;

public sealed partial class RadarPulseCliRebalanceBenchmarkTests
{
    [Fact]
    public void UsageNamesArchiveRebalanceDefaultsFallbackAndControlledProofBoundary()
    {
        var result = RunCli();

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Usage:", result.StandardOutput);
        Assert.Contains("radarpulse processing benchmark rebalance-archive", result.StandardOutput);
        Assert.Contains("radarpulse processing benchmark ordered-archive-processing", result.StandardOutput);
        Assert.Contains(
            "rebalance-archive omitted-provider default: queued-owned + pooled-copy + producer-consumer, async workers 4, queue capacity 8, retained-byte budget 536870912, retained-payload prewarm on.",
            result.StandardOutput);
        Assert.Contains(
            "rebalance-archive fallback/oracle: use --provider blocking-borrowed for the borrowed path and same-run comparison.",
            result.StandardOutput);
        Assert.Contains(
            "rebalance-archive direct MeasureFile()/MeasureCache() defaults use the same queued-owned rollout contour.",
            result.StandardOutput);
        Assert.Contains(
            "ordered-archive-processing uses the runtime/archive MVP path; handler-free rows use RunProcessingAsync and handler rows use RunMvpProcessingAsync.",
            result.StandardOutput);
        Assert.Contains(
            "--overlap-consumer-delay-ms is controlled mechanics proof, not natural rollout evidence.",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

}
