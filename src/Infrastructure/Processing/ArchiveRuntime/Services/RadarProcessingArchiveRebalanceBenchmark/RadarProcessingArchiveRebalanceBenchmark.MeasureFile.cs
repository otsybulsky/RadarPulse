using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveRebalanceBenchmark
{
    public RadarProcessingArchiveRebalanceBenchmarkResult MeasureFile(
        string filePath,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        int iterations,
        int warmupIterations,
        int partitionCount,
        int shardCount,
        int degreeOfParallelism,
        CancellationToken cancellationToken = default,
        RadarProcessingRebalanceHardeningOptions? hardeningOptions = null,
        RadarProcessingPressureSkewOptions? pressureSkewOptions = null,
        RadarProcessingExecutionMode? executionMode = null,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null,
        RadarProcessingArchiveProviderMode? providerMode = null,
        int? queueCapacity = null,
        TimeSpan? queueTimeout = null,
        RadarProcessingQueuedProviderOverlapMode? providerOverlapMode = null,
        RadarProcessingRetainedPayloadStrategy? retentionStrategy = null,
        long? queueRetainedPayloadBytes = null,
        TimeSpan overlapConsumerDelay = default,
        RadarProcessingRetainedPayloadFactory? retainedPayloadFactory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var plan = CreateArchiveBenchmarkMeasurementPlan(
            mode,
            iterations,
            warmupIterations,
            partitionCount,
            shardCount,
            degreeOfParallelism,
            hardeningOptions,
            pressureSkewOptions,
            executionMode,
            asyncExecution,
            providerMode,
            queueCapacity,
            queueTimeout,
            providerOverlapMode,
            retentionStrategy,
            queueRetainedPayloadBytes,
            overlapConsumerDelay,
            retainedPayloadFactory);

        if (partitionCount < shardCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(partitionCount),
                partitionCount,
                "Partition count must be greater than or equal to shard count.");
        }

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("NEXRAD archive file was not found.", filePath);
        }

        var sourceUniverse = ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse;
        if (partitionCount > sourceUniverse.SourceCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(partitionCount),
                partitionCount,
                "Partition count must be less than or equal to source count.");
        }

        var publishOptions = new ArchiveRadarEventBatchPublishOptions(sourceUniverse, degreeOfParallelism);
        using var archiveSession = new NexradArchiveRadarEventBatchPublishSession(
            decompressor,
            publishOptions);
        var workerTelemetryRecorder = plan.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport
            ? new RadarProcessingWorkerTelemetryRecorder(plan.HardeningOptions.TelemetryRetention)
            : null;
        RadarProcessingAsyncWorkerGroup? workerGroup = null;
        try
        {
            workerGroup = plan.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport
                ? new RadarProcessingAsyncWorkerGroup(
                    new RadarProcessingAsyncWorkerGroupOptions(plan.AsyncExecution))
                : null;

            for (var warmupIteration = 0; warmupIteration < warmupIterations; warmupIteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RunIteration(
                    archiveSession,
                    fileInfo.FullName,
                    sourceUniverse,
                    mode,
                    partitionCount,
                    shardCount,
                    plan.HardeningOptions,
                    plan.PressureSkewOptions,
                    plan.ExecutionMode,
                    plan.AsyncExecution,
                    workerTelemetryRecorder: null,
                    workerGroup,
                    plan.ProviderMode,
                    plan.QueueCapacity,
                    plan.QueueTimeout,
                    plan.ProviderOverlapMode,
                    plan.RetentionStrategy,
                    plan.QueueRetainedPayloadBytes,
                    plan.OverlapConsumerDelay,
                    plan.RetainedPayloadFactory,
                    cancellationToken);
            }

            var allocationBefore = RadarProcessingBenchmarkAllocationSnapshot.Capture();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            ArchiveIterationTelemetry? expectedIteration = null;
            var aggregate = ArchiveIterationTelemetry.Empty;
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var iterationTelemetry = RunIteration(
                    archiveSession,
                    fileInfo.FullName,
                    sourceUniverse,
                    mode,
                    partitionCount,
                    shardCount,
                    plan.HardeningOptions,
                    plan.PressureSkewOptions,
                    plan.ExecutionMode,
                    plan.AsyncExecution,
                    workerTelemetryRecorder,
                    workerGroup,
                    plan.ProviderMode,
                    plan.QueueCapacity,
                    plan.QueueTimeout,
                    plan.ProviderOverlapMode,
                    plan.RetentionStrategy,
                    plan.QueueRetainedPayloadBytes,
                    plan.OverlapConsumerDelay,
                    plan.RetainedPayloadFactory,
                    cancellationToken);
                if (expectedIteration.HasValue && !expectedIteration.Value.HasSameStableTotals(iterationTelemetry))
                {
                    throw new InvalidDataException("Archive rebalance benchmark produced inconsistent iteration totals.");
                }

                expectedIteration ??= iterationTelemetry;
                aggregate = aggregate.Add(iterationTelemetry);
            }

            stopwatch.Stop();
            var allocatedBytes = RadarProcessingBenchmarkAllocationSnapshot.Capture().DeltaSince(allocationBefore);
            var allocationSummary = RadarProcessingRebalanceAllocationSummary.ForArchiveReplay(
                allocatedBytes,
                aggregate.ProcessingCallbackAllocatedBytes,
                aggregate.QueueTelemetry.OwnedSnapshotAllocatedBytes);
            var measuredIteration = expectedIteration ??
                                    throw new InvalidOperationException("Archive rebalance benchmark did not run.");
            var workerTelemetry = workerTelemetryRecorder?.CreateSummary();
            ValidateWorkerTelemetry(workerTelemetry, workerTelemetryRecorder, plan.HardeningOptions);

            return new RadarProcessingArchiveRebalanceBenchmarkResult(
                fileInfo.FullName,
                decompressor.Name,
                mode,
                iterations,
                warmupIterations,
                degreeOfParallelism,
                sourceUniverse.SourceCount,
                partitionCount,
                shardCount,
                measuredIteration.FileSizeBytes,
                measuredIteration.CompressedRecordCount,
                measuredIteration.CompressedBytes,
                measuredIteration.DecompressedBytes,
                measuredIteration.BatchCount,
                measuredIteration.EventCount,
                measuredIteration.PayloadBytes,
                measuredIteration.PayloadValueCount,
                measuredIteration.RawValueChecksum,
                measuredIteration.TopologyVersionCount,
                aggregate.RebalanceEvaluationCount,
                aggregate.AcceptedMoveCount,
                aggregate.SkippedDecisionCount,
                aggregate.DirectHotReliefCount,
                aggregate.ColdEvacuationCount,
                aggregate.FailedMigrationCount,
                aggregate.ValidationSucceeded,
                aggregate.ValidationChecksum,
                CreateReadOnlyList(aggregate.SkippedReasons),
                CreateSortedSkippedReasonCounters(aggregate.SkippedReasonCounters),
                CreateReadOnlyList(aggregate.AcceptedMovePressures),
                aggregate.RetentionStats,
                stopwatch.Elapsed,
                aggregate.ProcessingElapsed,
                allocatedBytes,
                plan.HardeningOptions.ValidationProfile,
                plan.HardeningOptions.TelemetryRetention.RetentionMode,
                plan.HardeningOptions.QuarantineLifecycle.QuarantineTtlEvaluations,
                plan.HardeningOptions.QuarantineLifecycle.SustainedCoolingSampleCount,
                plan.HardeningOptions.QuarantineLifecycle.MaterialPressureChangeThreshold,
                plan.HardeningOptions.TelemetryRetention.MaxRetainedDecisions,
                plan.HardeningOptions.TelemetryRetention.MaxRetainedLifecycleTransitions,
                plan.HardeningOptions.TelemetryRetention.MaxRetainedAcceptedMoves,
                plan.HardeningOptions.TelemetryRetention.MaxRetainedValidationFailures,
                plan.PressureSkewOptions,
                allocationSummary,
                plan.ExecutionMode,
                workerTelemetry,
                plan.ProviderMode,
                plan.ProviderMode == RadarProcessingArchiveProviderMode.QueuedOwned ? plan.QueueCapacity : 0,
                plan.ProviderOverlapMode,
                plan.RetentionStrategy,
                plan.ProviderMode == RadarProcessingArchiveProviderMode.QueuedOwned ? plan.QueueRetainedPayloadBytes : null,
                aggregate.QueueTelemetry,
                aggregate.RetentionTelemetry,
                aggregate.OverlapTelemetry,
                plan.OverlapConsumerDelay,
                plan.DefaultRetainedPayloadPrewarm?.Result,
                aggregate.ProcessingValidationFailedBatchCount);
        }
        finally
        {
            if (workerGroup is not null)
            {
                workerGroup.DisposeAsync().GetAwaiter().GetResult();
            }
        }
    }

}
