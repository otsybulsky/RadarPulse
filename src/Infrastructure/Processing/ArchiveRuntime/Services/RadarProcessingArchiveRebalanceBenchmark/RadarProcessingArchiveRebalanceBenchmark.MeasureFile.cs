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
        EnsureKnownMode(mode);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);
        ArgumentOutOfRangeException.ThrowIfNegative(warmupIterations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(partitionCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shardCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(degreeOfParallelism);
        ValidateQueueTimeout(queueTimeout);
        ValidateQueueRetainedPayloadBytes(queueRetainedPayloadBytes);
        ValidateOverlapConsumerDelay(overlapConsumerDelay);

        var useRolloutDefaults = !providerMode.HasValue;
        var effectiveProviderMode = providerMode ?? RadarProcessingArchiveRebalanceRolloutDefaults.ProviderMode;
        var effectiveExecutionMode = executionMode ??
                                     (useRolloutDefaults
                                         ? RadarProcessingArchiveRebalanceRolloutDefaults.ExecutionMode
                                         : RadarProcessingExecutionMode.PartitionedBarrier);
        var effectiveQueueCapacity = queueCapacity ??
                                     (useRolloutDefaults
                                         ? RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity
                                         : 1);
        var effectiveProviderOverlapMode = providerOverlapMode ??
                                           (useRolloutDefaults
                                               ? RadarProcessingArchiveRebalanceRolloutDefaults.ProviderOverlapMode
                                               : RadarProcessingQueuedProviderOverlapMode.None);
        var effectiveRetentionStrategy = retentionStrategy ??
                                         (useRolloutDefaults
                                             ? RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy
                                             : RadarProcessingRetainedPayloadStrategy.SnapshotCopy);
        var effectiveQueueRetainedPayloadBytes = queueRetainedPayloadBytes ??
                                                 (useRolloutDefaults
                                                     ? RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes
                                                     : null);

        EnsureKnownExecutionMode(effectiveExecutionMode);
        EnsureKnownProviderMode(effectiveProviderMode);
        EnsureKnownProviderOverlapMode(effectiveProviderOverlapMode);
        RadarProcessingRetainedPayloadOptions.EnsureKnownStrategy(effectiveRetentionStrategy);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(effectiveQueueCapacity);
        ValidateQueuedProviderControls(
            effectiveProviderMode,
            effectiveProviderOverlapMode,
            effectiveRetentionStrategy,
            effectiveQueueRetainedPayloadBytes,
            overlapConsumerDelay);

        if (partitionCount < shardCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(partitionCount),
                partitionCount,
                "Partition count must be greater than or equal to shard count.");
        }

        var effectiveHardeningOptions = hardeningOptions ?? RadarProcessingRebalanceHardeningOptions.Default;
        var effectiveAsyncExecution = effectiveExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport
            ? asyncExecution ?? (useRolloutDefaults
                ? RadarProcessingArchiveRebalanceRolloutDefaults.CreateAsyncExecution()
                : new RadarProcessingAsyncExecutionOptions(workerCount: shardCount, queueCapacity: 1))
            : asyncExecution;
        var defaultRetainedPayloadPrewarm = CreateDefaultRetainedPayloadPrewarm(
            effectiveProviderMode,
            effectiveProviderOverlapMode,
            effectiveRetentionStrategy,
            effectiveExecutionMode,
            effectiveAsyncExecution,
            effectiveQueueCapacity,
            effectiveQueueRetainedPayloadBytes,
            overlapConsumerDelay,
            retainedPayloadFactory);
        var effectiveRetainedPayloadFactory =
            defaultRetainedPayloadPrewarm?.Factory ?? retainedPayloadFactory;

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
        var workerTelemetryRecorder = effectiveExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport
            ? new RadarProcessingWorkerTelemetryRecorder(effectiveHardeningOptions.TelemetryRetention)
            : null;
        RadarProcessingAsyncWorkerGroup? workerGroup = null;
        try
        {
            workerGroup = effectiveExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport
                ? new RadarProcessingAsyncWorkerGroup(
                    new RadarProcessingAsyncWorkerGroupOptions(effectiveAsyncExecution))
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
                    effectiveHardeningOptions,
                    pressureSkewOptions ?? RadarProcessingPressureSkewOptions.None,
                    effectiveExecutionMode,
                    effectiveAsyncExecution,
                    workerTelemetryRecorder: null,
                    workerGroup,
                    effectiveProviderMode,
                    effectiveQueueCapacity,
                    queueTimeout,
                    effectiveProviderOverlapMode,
                    effectiveRetentionStrategy,
                    effectiveQueueRetainedPayloadBytes,
                    overlapConsumerDelay,
                    effectiveRetainedPayloadFactory,
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
                    effectiveHardeningOptions,
                    pressureSkewOptions ?? RadarProcessingPressureSkewOptions.None,
                    effectiveExecutionMode,
                    effectiveAsyncExecution,
                    workerTelemetryRecorder,
                    workerGroup,
                    effectiveProviderMode,
                    effectiveQueueCapacity,
                    queueTimeout,
                    effectiveProviderOverlapMode,
                    effectiveRetentionStrategy,
                    effectiveQueueRetainedPayloadBytes,
                    overlapConsumerDelay,
                    effectiveRetainedPayloadFactory,
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
            ValidateWorkerTelemetry(workerTelemetry, workerTelemetryRecorder, effectiveHardeningOptions);

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
                effectiveHardeningOptions.ValidationProfile,
                effectiveHardeningOptions.TelemetryRetention.RetentionMode,
                effectiveHardeningOptions.QuarantineLifecycle.QuarantineTtlEvaluations,
                effectiveHardeningOptions.QuarantineLifecycle.SustainedCoolingSampleCount,
                effectiveHardeningOptions.QuarantineLifecycle.MaterialPressureChangeThreshold,
                effectiveHardeningOptions.TelemetryRetention.MaxRetainedDecisions,
                effectiveHardeningOptions.TelemetryRetention.MaxRetainedLifecycleTransitions,
                effectiveHardeningOptions.TelemetryRetention.MaxRetainedAcceptedMoves,
                effectiveHardeningOptions.TelemetryRetention.MaxRetainedValidationFailures,
                pressureSkewOptions ?? RadarProcessingPressureSkewOptions.None,
                allocationSummary,
                effectiveExecutionMode,
                workerTelemetry,
                effectiveProviderMode,
                effectiveProviderMode == RadarProcessingArchiveProviderMode.QueuedOwned ? effectiveQueueCapacity : 0,
                effectiveProviderOverlapMode,
                effectiveRetentionStrategy,
                effectiveProviderMode == RadarProcessingArchiveProviderMode.QueuedOwned ? effectiveQueueRetainedPayloadBytes : null,
                aggregate.QueueTelemetry,
                aggregate.RetentionTelemetry,
                aggregate.OverlapTelemetry,
                overlapConsumerDelay,
                defaultRetainedPayloadPrewarm?.Result,
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

    /// <summary>
    /// Measures archive rebalance behavior over a bounded cache selection using explicit or rollout-default adapters.
    /// </summary>
}
