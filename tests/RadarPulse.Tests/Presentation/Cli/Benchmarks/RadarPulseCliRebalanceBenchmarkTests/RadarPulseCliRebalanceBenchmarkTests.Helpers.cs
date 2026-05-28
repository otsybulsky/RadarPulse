using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Presentation;

public sealed partial class RadarPulseCliRebalanceBenchmarkTests
{
    private static void AssertRolloutContour(
        global::ProcessingBenchmarkArchiveRebalanceOptions options,
        ProcessingBenchmarkOptionValueSource providerModeSource,
        ProcessingBenchmarkOptionValueSource providerOverlapModeSource,
        ProcessingBenchmarkOptionValueSource retentionStrategySource,
        ProcessingBenchmarkOptionValueSource queueCapacitySource,
        ProcessingBenchmarkOptionValueSource queueRetainedPayloadBytesSource,
        ProcessingBenchmarkOptionValueSource queueTelemetrySource,
        ProcessingBenchmarkOptionValueSource overlapTelemetrySource,
        ProcessingBenchmarkOptionValueSource overlapConsumerDelaySource,
        ProcessingBenchmarkOptionValueSource executionModeSource,
        ProcessingBenchmarkOptionValueSource workerCountSource,
        bool isRolloutDefaultExpandedContour)
    {
        Assert.Equal(RadarProcessingArchiveProviderMode.QueuedOwned, options.ProviderMode);
        Assert.Equal(
            RadarProcessingQueuedProviderOverlapMode.ProducerConsumer,
            options.ProviderOverlapMode);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, options.RetentionStrategy);
        Assert.Equal(
            global::ProcessingBenchmarkArchiveRebalanceOptions.DefaultRolloutProviderQueueCapacity,
            options.ProviderQueueCapacity);
        Assert.Equal(
            global::ProcessingBenchmarkArchiveRebalanceOptions.DefaultRolloutRetainedPayloadBytes,
            options.ProviderQueueRetainedPayloadBytes);
        Assert.Equal(TimeSpan.Zero, options.OverlapConsumerDelay);
        Assert.Equal(ProcessingBenchmarkProviderQueueTelemetryOutput.Summary, options.QueueTelemetryOutput);
        Assert.Equal(ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary, options.OverlapTelemetryOutput);
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, options.ExecutionMode);
        Assert.NotNull(options.AsyncExecution);
        Assert.Equal(
            global::ProcessingBenchmarkArchiveRebalanceOptions.DefaultRolloutWorkerCount,
            options.AsyncExecution.WorkerCount);
        Assert.Equal(
            global::ProcessingBenchmarkArchiveRebalanceOptions.DefaultRolloutProviderQueueCapacity,
            options.AsyncExecution.QueueCapacity);
        Assert.True(options.IsDefaultCandidateContour);
        Assert.True(
            global::ProcessingBenchmarkArchiveRebalanceOptions.MatchesDefaultCandidateContour(
                options.ProviderMode,
                options.ProviderQueueCapacity,
                options.ProviderOverlapMode,
                options.RetentionStrategy,
                options.ProviderQueueRetainedPayloadBytes,
                options.OverlapConsumerDelay,
                options.QueueTelemetryOutput,
                options.OverlapTelemetryOutput,
                options.ExecutionMode));
        Assert.False(options.IsControlledProviderOverlapProof);
        Assert.Equal(
            global::ProcessingBenchmarkArchiveRebalanceOptions.NaturalDefaultCandidateEvidenceContour,
            options.ProviderOverlapEvidenceContour);
        Assert.Equal(
            global::ProcessingBenchmarkArchiveRebalanceOptions.NaturalReadinessEvidenceScope,
            options.ProviderOverlapEvidenceScope);
        Assert.Equal(providerModeSource, options.EffectiveOptionProvenance.ProviderMode);
        Assert.Equal(providerOverlapModeSource, options.EffectiveOptionProvenance.ProviderOverlapMode);
        Assert.Equal(retentionStrategySource, options.EffectiveOptionProvenance.RetentionStrategy);
        Assert.Equal(queueCapacitySource, options.EffectiveOptionProvenance.QueueCapacity);
        Assert.Equal(queueRetainedPayloadBytesSource, options.EffectiveOptionProvenance.QueueRetainedPayloadBytes);
        Assert.Equal(queueTelemetrySource, options.EffectiveOptionProvenance.QueueTelemetry);
        Assert.Equal(overlapTelemetrySource, options.EffectiveOptionProvenance.OverlapTelemetry);
        Assert.Equal(overlapConsumerDelaySource, options.EffectiveOptionProvenance.OverlapConsumerDelay);
        Assert.Equal(executionModeSource, options.EffectiveOptionProvenance.ExecutionMode);
        Assert.Equal(workerCountSource, options.EffectiveOptionProvenance.WorkerCount);
        Assert.False(options.IsExplicitBlockingBorrowedFallback);
        Assert.Equal(isRolloutDefaultExpandedContour, options.IsRolloutDefaultExpandedContour);
    }

    private static void AssertExplicitBorrowedFallbackContour(
        global::ProcessingBenchmarkArchiveRebalanceOptions options)
    {
        Assert.Equal(RadarProcessingArchiveProviderMode.BlockingBorrowed, options.ProviderMode);
        Assert.Equal(RadarProcessingQueuedProviderOverlapMode.None, options.ProviderOverlapMode);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.SnapshotCopy, options.RetentionStrategy);
        Assert.Equal(1, options.ProviderQueueCapacity);
        Assert.Null(options.ProviderQueueRetainedPayloadBytes);
        Assert.Equal(TimeSpan.Zero, options.OverlapConsumerDelay);
        Assert.Equal(ProcessingBenchmarkProviderQueueTelemetryOutput.Summary, options.QueueTelemetryOutput);
        Assert.Equal(ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary, options.OverlapTelemetryOutput);
        Assert.Equal(RadarProcessingExecutionMode.PartitionedBarrier, options.ExecutionMode);
        Assert.Null(options.AsyncExecution);
        Assert.False(options.IsDefaultCandidateContour);
        Assert.False(options.IsControlledProviderOverlapProof);
        Assert.Equal(
            global::ProcessingBenchmarkArchiveRebalanceOptions.NotApplicableEvidenceContour,
            options.ProviderOverlapEvidenceContour);
        Assert.Equal(
            global::ProcessingBenchmarkArchiveRebalanceOptions.NotApplicableEvidenceScope,
            options.ProviderOverlapEvidenceScope);
        Assert.Equal(ProcessingBenchmarkOptionValueSource.Explicit, options.EffectiveOptionProvenance.ProviderMode);
        Assert.Equal(
            ProcessingBenchmarkOptionValueSource.CurrentDefault,
            options.EffectiveOptionProvenance.ProviderOverlapMode);
        Assert.Equal(
            ProcessingBenchmarkOptionValueSource.CurrentDefault,
            options.EffectiveOptionProvenance.RetentionStrategy);
        Assert.Equal(
            ProcessingBenchmarkOptionValueSource.CurrentDefault,
            options.EffectiveOptionProvenance.QueueCapacity);
        Assert.Equal(
            ProcessingBenchmarkOptionValueSource.CurrentDefault,
            options.EffectiveOptionProvenance.QueueRetainedPayloadBytes);
        Assert.Equal(
            ProcessingBenchmarkOptionValueSource.CurrentDefault,
            options.EffectiveOptionProvenance.QueueTelemetry);
        Assert.Equal(
            ProcessingBenchmarkOptionValueSource.CurrentDefault,
            options.EffectiveOptionProvenance.OverlapTelemetry);
        Assert.Equal(
            ProcessingBenchmarkOptionValueSource.CurrentDefault,
            options.EffectiveOptionProvenance.OverlapConsumerDelay);
        Assert.Equal(
            ProcessingBenchmarkOptionValueSource.CurrentDefault,
            options.EffectiveOptionProvenance.ExecutionMode);
        Assert.Equal(
            ProcessingBenchmarkOptionValueSource.CurrentDefault,
            options.EffectiveOptionProvenance.WorkerCount);
        Assert.True(options.IsExplicitBlockingBorrowedFallback);
        Assert.False(options.IsRolloutDefaultExpandedContour);
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
