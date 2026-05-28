using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Presentation;

public sealed partial class RadarPulseCliRebalanceBenchmarkTests
{
    [Fact]
    public void ArchiveRebalanceBenchmarkOptionsIdentifyDefaultCandidateContour()
    {
        var options = global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
        [
            "--cache",
            "data/nexrad",
            "--provider",
            "queued-owned",
            "--provider-overlap",
            "producer-consumer",
            "--retention-strategy",
            "pooled-copy",
            "--execution",
            "async",
            "--workers",
            "4",
            "--queue-capacity",
            "8",
            "--queue-retained-bytes",
            "536870912",
            "--queue-telemetry",
            "summary",
            "--overlap-telemetry",
            "summary",
            "--mode",
            "rebalance"
        ]);

        AssertRolloutContour(
            options,
            ProcessingBenchmarkOptionValueSource.Explicit,
            ProcessingBenchmarkOptionValueSource.Explicit,
            ProcessingBenchmarkOptionValueSource.Explicit,
            ProcessingBenchmarkOptionValueSource.Explicit,
            ProcessingBenchmarkOptionValueSource.Explicit,
            ProcessingBenchmarkOptionValueSource.Explicit,
            ProcessingBenchmarkOptionValueSource.Explicit,
            ProcessingBenchmarkOptionValueSource.CurrentDefault,
            ProcessingBenchmarkOptionValueSource.Explicit,
            ProcessingBenchmarkOptionValueSource.Explicit,
            isRolloutDefaultExpandedContour: false);
    }

    [Fact]
    public void ArchiveRebalanceBenchmarkOptionsExpandOmittedProviderToRolloutDefaults()
    {
        var options = global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
        [
            "--cache",
            "data/nexrad",
            "--mode",
            "rebalance"
        ]);

        AssertRolloutContour(
            options,
            ProcessingBenchmarkOptionValueSource.RolloutDefault,
            ProcessingBenchmarkOptionValueSource.RolloutDefault,
            ProcessingBenchmarkOptionValueSource.RolloutDefault,
            ProcessingBenchmarkOptionValueSource.RolloutDefault,
            ProcessingBenchmarkOptionValueSource.RolloutDefault,
            ProcessingBenchmarkOptionValueSource.RolloutDefault,
            ProcessingBenchmarkOptionValueSource.RolloutDefault,
            ProcessingBenchmarkOptionValueSource.RolloutDefault,
            ProcessingBenchmarkOptionValueSource.RolloutDefault,
            ProcessingBenchmarkOptionValueSource.RolloutDefault,
            isRolloutDefaultExpandedContour: true);
    }

    [Fact]
    public void ArchiveRebalanceBenchmarkOptionsTrackExplicitBorrowedFallbackProvenance()
    {
        var options = global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
        [
            "--cache",
            "data/nexrad",
            "--mode",
            "rebalance",
            "--provider",
            "blocking-borrowed"
        ]);

        AssertExplicitBorrowedFallbackContour(options);
    }

    [Fact]
    public void ArchiveRebalanceBenchmarkOptionsExposeRolloutDefaultContourConstants()
    {
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount,
            global::ProcessingBenchmarkArchiveRebalanceOptions.DefaultRolloutWorkerCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity,
            global::ProcessingBenchmarkArchiveRebalanceOptions.DefaultCandidateProviderQueueCapacity);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes,
            global::ProcessingBenchmarkArchiveRebalanceOptions.DefaultCandidateRetainedPayloadBytes);
        Assert.Equal(
            global::ProcessingBenchmarkArchiveRebalanceOptions.DefaultCandidateProviderQueueCapacity,
            global::ProcessingBenchmarkArchiveRebalanceOptions.DefaultRolloutProviderQueueCapacity);
        Assert.Equal(
            global::ProcessingBenchmarkArchiveRebalanceOptions.DefaultCandidateRetainedPayloadBytes,
            global::ProcessingBenchmarkArchiveRebalanceOptions.DefaultRolloutRetainedPayloadBytes);
    }

    [Fact]
    public void ArchiveRebalanceBenchmarkOptionsRolloutContourMatchesSharedContract()
    {
        var options = global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
        [
            "--cache",
            "data/nexrad",
            "--mode",
            "rebalance"
        ]);

        Assert.True(
            RadarProcessingArchiveRebalanceRolloutDefaults.Matches(
                options.ProviderMode,
                options.ProviderOverlapMode,
                options.RetentionStrategy,
                options.ExecutionMode,
                options.AsyncExecution,
                options.ProviderQueueCapacity,
                options.ProviderQueueRetainedPayloadBytes,
                options.OverlapConsumerDelay));
    }

}
