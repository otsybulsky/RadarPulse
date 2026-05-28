using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Presentation;

public sealed partial class RadarPulseCliRebalanceBenchmarkTests
{
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
            "--provider",
            "blocking-borrowed",
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
        Assert.Equal(RadarProcessingQueuedProviderOverlapMode.None, options.ProviderOverlapMode);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.SnapshotCopy, options.RetentionStrategy);
        Assert.Null(options.ProviderQueueRetainedPayloadBytes);
        Assert.Equal(TimeSpan.Zero, options.OverlapConsumerDelay);
        Assert.Equal(ProcessingBenchmarkProviderQueueTelemetryOutput.Summary, options.QueueTelemetryOutput);
        Assert.Equal(ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary, options.OverlapTelemetryOutput);
        Assert.False(options.IsDefaultCandidateContour);
        Assert.False(options.IsControlledProviderOverlapProof);
        Assert.Equal("not-applicable", options.ProviderOverlapEvidenceContour);
        Assert.Equal("not-applicable", options.ProviderOverlapEvidenceScope);
        Assert.Equal(ProcessingBenchmarkOptionValueSource.Explicit, options.EffectiveOptionProvenance.ProviderMode);
        Assert.Equal(ProcessingBenchmarkOptionValueSource.CurrentDefault, options.EffectiveOptionProvenance.ProviderOverlapMode);
        Assert.Equal(ProcessingBenchmarkOptionValueSource.CurrentDefault, options.EffectiveOptionProvenance.RetentionStrategy);
        Assert.Equal(ProcessingBenchmarkOptionValueSource.Explicit, options.EffectiveOptionProvenance.QueueCapacity);
        Assert.Equal(ProcessingBenchmarkOptionValueSource.CurrentDefault, options.EffectiveOptionProvenance.QueueRetainedPayloadBytes);
        Assert.Equal(ProcessingBenchmarkOptionValueSource.CurrentDefault, options.EffectiveOptionProvenance.QueueTelemetry);
        Assert.Equal(ProcessingBenchmarkOptionValueSource.CurrentDefault, options.EffectiveOptionProvenance.OverlapTelemetry);
        Assert.Equal(ProcessingBenchmarkOptionValueSource.CurrentDefault, options.EffectiveOptionProvenance.OverlapConsumerDelay);
        Assert.Equal(ProcessingBenchmarkOptionValueSource.Explicit, options.EffectiveOptionProvenance.ExecutionMode);
        Assert.Equal(ProcessingBenchmarkOptionValueSource.Explicit, options.EffectiveOptionProvenance.WorkerCount);
        Assert.True(options.IsExplicitBlockingBorrowedFallback);
        Assert.False(options.IsRolloutDefaultExpandedContour);
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
            "--provider-overlap",
            "producer-consumer",
            "--retention-strategy",
            "pooled-copy",
            "--queue-retained-bytes",
            "4096",
            "--overlap-consumer-delay-ms",
            "25",
            "--queue-telemetry",
            "recent",
            "--overlap-telemetry",
            "recent",
            "--mode",
            "static"
        ]);

        Assert.Equal(RadarProcessingArchiveProviderMode.QueuedOwned, options.ProviderMode);
        Assert.Equal(3, options.ProviderQueueCapacity);
        Assert.Equal(TimeSpan.FromMilliseconds(250), options.ProviderQueueTimeout);
        Assert.Equal(RadarProcessingQueuedProviderOverlapMode.ProducerConsumer, options.ProviderOverlapMode);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, options.RetentionStrategy);
        Assert.Equal(4096, options.ProviderQueueRetainedPayloadBytes);
        Assert.Equal(TimeSpan.FromMilliseconds(25), options.OverlapConsumerDelay);
        Assert.Equal(ProcessingBenchmarkProviderQueueTelemetryOutput.Recent, options.QueueTelemetryOutput);
        Assert.Equal(ProcessingBenchmarkProviderOverlapTelemetryOutput.Recent, options.OverlapTelemetryOutput);
        Assert.False(options.IsDefaultCandidateContour);
        Assert.True(options.IsControlledProviderOverlapProof);
        Assert.Equal("controlled-proof", options.ProviderOverlapEvidenceContour);
        Assert.Equal("controlled-mechanics-proof", options.ProviderOverlapEvidenceScope);
        Assert.Equal(RadarProcessingExecutionMode.PartitionedBarrier, options.ExecutionMode);
        Assert.Null(options.AsyncExecution);
        Assert.Equal(ProcessingBenchmarkOptionValueSource.Explicit, options.EffectiveOptionProvenance.ProviderMode);
        Assert.Equal(ProcessingBenchmarkOptionValueSource.Explicit, options.EffectiveOptionProvenance.ProviderOverlapMode);
        Assert.Equal(ProcessingBenchmarkOptionValueSource.Explicit, options.EffectiveOptionProvenance.RetentionStrategy);
        Assert.Equal(ProcessingBenchmarkOptionValueSource.Explicit, options.EffectiveOptionProvenance.QueueCapacity);
        Assert.Equal(ProcessingBenchmarkOptionValueSource.Explicit, options.EffectiveOptionProvenance.QueueRetainedPayloadBytes);
        Assert.Equal(ProcessingBenchmarkOptionValueSource.Explicit, options.EffectiveOptionProvenance.QueueTelemetry);
        Assert.Equal(ProcessingBenchmarkOptionValueSource.Explicit, options.EffectiveOptionProvenance.OverlapTelemetry);
        Assert.Equal(ProcessingBenchmarkOptionValueSource.Explicit, options.EffectiveOptionProvenance.OverlapConsumerDelay);
        Assert.Equal(ProcessingBenchmarkOptionValueSource.CurrentDefault, options.EffectiveOptionProvenance.ExecutionMode);
        Assert.Equal(ProcessingBenchmarkOptionValueSource.CurrentDefault, options.EffectiveOptionProvenance.WorkerCount);
        Assert.False(options.IsExplicitBlockingBorrowedFallback);
        Assert.False(options.IsRolloutDefaultExpandedContour);
    }

}
