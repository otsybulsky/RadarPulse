using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Measures archive replay with rebalance processing across provider and execution modes.
/// </summary>
public sealed class RadarProcessingArchiveRebalanceBenchmark
{
    private const ulong ChecksumInitial = 14_695_981_039_346_656_037UL;
    private const ulong ChecksumPrime = 1_099_511_628_211UL;
    private const int MaxAutoSizedCacheRadarOrdinalCount = 256;

    private readonly IArchiveBZip2Decompressor decompressor;

    /// <summary>
    /// Creates a benchmark with the default archive decompressor.
    /// </summary>
    public RadarProcessingArchiveRebalanceBenchmark()
        : this(ArchiveBZip2Decompressors.Create(ArchiveBZip2Decompressors.DefaultName))
    {
    }

    /// <summary>
    /// Creates a benchmark with an explicit archive decompressor.
    /// </summary>
    public RadarProcessingArchiveRebalanceBenchmark(IArchiveBZip2Decompressor decompressor)
    {
        this.decompressor = decompressor ?? throw new ArgumentNullException(nameof(decompressor));
    }

    /// <summary>
    /// Measures archive rebalance processing over one local archive file.
    /// </summary>
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
    public RadarProcessingArchiveRebalanceCacheBenchmarkResult MeasureCache(
        string cachePath,
        DateOnly? date,
        string? radarId,
        int maxFiles,
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
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFiles);
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

        var directoryInfo = new DirectoryInfo(cachePath);
        if (!directoryInfo.Exists)
        {
            throw new DirectoryNotFoundException($"NEXRAD cache directory was not found: {cachePath}");
        }

        var normalizedRadarId = string.IsNullOrWhiteSpace(radarId)
            ? null
            : HistoricalArchiveRequest.NormalizeRadarId(radarId);
        var sourceUniverse = CreateCacheSourceUniverse(
            directoryInfo,
            date,
            normalizedRadarId,
            maxFiles,
            cancellationToken);
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
                RunCacheIteration(
                    archiveSession,
                    directoryInfo,
                    date,
                    normalizedRadarId,
                    maxFiles,
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
                var iterationTelemetry = RunCacheIteration(
                    archiveSession,
                    directoryInfo,
                    date,
                    normalizedRadarId,
                    maxFiles,
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
                    throw new InvalidDataException("Archive cache rebalance benchmark produced inconsistent iteration totals.");
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
                                    throw new InvalidOperationException("Archive cache rebalance benchmark did not run.");
            var workerTelemetry = workerTelemetryRecorder?.CreateSummary();
            ValidateWorkerTelemetry(workerTelemetry, workerTelemetryRecorder, effectiveHardeningOptions);

            return new RadarProcessingArchiveRebalanceCacheBenchmarkResult(
                directoryInfo.FullName,
                date,
                normalizedRadarId,
                decompressor.Name,
                mode,
                iterations,
                warmupIterations,
                degreeOfParallelism,
                sourceUniverse.SourceCount,
                partitionCount,
                shardCount,
                measuredIteration.ExaminedFileCount,
                measuredIteration.SkippedFileCount,
                measuredIteration.PublishedFileCount,
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

    private static RadarSourceUniverse CreateCacheSourceUniverse(
        DirectoryInfo directoryInfo,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        CancellationToken cancellationToken)
    {
        if (radarId is not null)
        {
            return ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse;
        }

        var radarOrdinalCount = CountSelectedCacheRadarOrdinals(
            directoryInfo,
            date,
            radarId,
            maxFiles,
            cancellationToken);
        return CreateArchiveSourceUniverse(radarOrdinalCount);
    }

    private static int CountSelectedCacheRadarOrdinals(
        DirectoryInfo directoryInfo,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        CancellationToken cancellationToken)
    {
        var selection = SelectCacheArchiveFiles(directoryInfo, date, radarId, maxFiles, cancellationToken);
        if (selection.BaseDataFiles.Count == 0)
        {
            return ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse.RadarOrdinalCount;
        }

        var radarIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fileInfo in selection.BaseDataFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var header = ArchiveTwoFileReader.ReadVolumeHeader(fileInfo);
            radarIds.Add(header.RadarId);
            if (radarIds.Count > MaxAutoSizedCacheRadarOrdinalCount)
            {
                throw new InvalidOperationException(
                    $"Cache benchmark auto-sized source universe supports at most {MaxAutoSizedCacheRadarOrdinalCount} distinct radar ids. " +
                    "Pass a radar id filter or reduce max files.");
            }
        }

        return Math.Max(1, radarIds.Count);
    }

    private static RadarSourceUniverse CreateArchiveSourceUniverse(int radarOrdinalCount)
    {
        var defaultUniverse = ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse;
        if (radarOrdinalCount == defaultUniverse.RadarOrdinalCount)
        {
            return defaultUniverse;
        }

        return new RadarSourceUniverse(
            defaultUniverse.Version,
            radarOrdinalCount,
            defaultUniverse.ElevationSlotCount,
            defaultUniverse.AzimuthBucketCount,
            defaultUniverse.RangeBandCount);
    }

    private static DefaultRetainedPayloadPrewarm? CreateDefaultRetainedPayloadPrewarm(
        RadarProcessingArchiveProviderMode providerMode,
        RadarProcessingQueuedProviderOverlapMode providerOverlapMode,
        RadarProcessingRetainedPayloadStrategy retentionStrategy,
        RadarProcessingExecutionMode executionMode,
        RadarProcessingAsyncExecutionOptions? asyncExecution,
        int providerQueueCapacity,
        long? retainedPayloadBytes,
        TimeSpan overlapConsumerDelay,
        RadarProcessingRetainedPayloadFactory? retainedPayloadFactory)
    {
        if (!RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmEnabled ||
            retainedPayloadFactory is not null ||
            !RadarProcessingArchiveRebalanceRolloutDefaults.Matches(
                providerMode,
                providerOverlapMode,
                retentionStrategy,
                executionMode,
                asyncExecution,
                providerQueueCapacity,
                retainedPayloadBytes,
                overlapConsumerDelay))
        {
            return null;
        }

        var factory = new RadarProcessingRetainedPayloadFactory();
        var prewarm = factory.Prewarm(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmEventCount,
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmPayloadBytes,
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmBatchCount);
        return new DefaultRetainedPayloadPrewarm(factory, prewarm);
    }

    private static ArchiveIterationTelemetry RunIteration(
        NexradArchiveRadarEventBatchPublishSession archiveSession,
        string filePath,
        RadarSourceUniverse sourceUniverse,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        int partitionCount,
        int shardCount,
        RadarProcessingRebalanceHardeningOptions hardeningOptions,
        RadarProcessingPressureSkewOptions pressureSkewOptions,
        RadarProcessingExecutionMode executionMode,
        RadarProcessingAsyncExecutionOptions? asyncExecution,
        RadarProcessingWorkerTelemetryRecorder? workerTelemetryRecorder,
        RadarProcessingAsyncWorkerGroup? workerGroup,
        RadarProcessingArchiveProviderMode providerMode,
        int queueCapacity,
        TimeSpan? queueTimeout,
        RadarProcessingQueuedProviderOverlapMode providerOverlapMode,
        RadarProcessingRetainedPayloadStrategy retentionStrategy,
        long? queueRetainedPayloadBytes,
        TimeSpan overlapConsumerDelay,
        RadarProcessingRetainedPayloadFactory? retainedPayloadFactory,
        CancellationToken cancellationToken)
    {
        using var processor = new ArchiveRebalanceBatchProcessor(
            sourceUniverse,
            mode,
            partitionCount,
            shardCount,
            hardeningOptions,
            pressureSkewOptions,
            executionMode,
            asyncExecution,
            workerTelemetryRecorder,
            workerGroup);
        if (providerMode == RadarProcessingArchiveProviderMode.BlockingBorrowed)
        {
            var publishResult = archiveSession.PublishFile(filePath, processor, cancellationToken);
            return processor.BuildTelemetry(publishResult);
        }

        var queuedResult = providerOverlapMode == RadarProcessingQueuedProviderOverlapMode.ProducerConsumer
            ? PublishFileQueuedOwnedOverlap(
                archiveSession,
                filePath,
                processor,
                queueCapacity,
                queueTimeout,
                retentionStrategy,
                queueRetainedPayloadBytes,
                overlapConsumerDelay,
                retainedPayloadFactory,
                cancellationToken)
            : PublishFileQueuedOwned(
                archiveSession,
                filePath,
                processor,
                queueCapacity,
                queueTimeout,
                retentionStrategy,
                queueRetainedPayloadBytes,
                retainedPayloadFactory,
                cancellationToken);
        return processor
            .BuildTelemetry(queuedResult.PublishResult)
            .WithQueueTelemetry(queuedResult.QueueTelemetry)
            .WithRetentionTelemetry(queuedResult.RetentionTelemetry)
            .WithOverlapTelemetry(queuedResult.OverlapTelemetry);
    }

    private static ArchiveIterationTelemetry RunCacheIteration(
        NexradArchiveRadarEventBatchPublishSession archiveSession,
        DirectoryInfo directoryInfo,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        RadarSourceUniverse sourceUniverse,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        int partitionCount,
        int shardCount,
        RadarProcessingRebalanceHardeningOptions hardeningOptions,
        RadarProcessingPressureSkewOptions pressureSkewOptions,
        RadarProcessingExecutionMode executionMode,
        RadarProcessingAsyncExecutionOptions? asyncExecution,
        RadarProcessingWorkerTelemetryRecorder? workerTelemetryRecorder,
        RadarProcessingAsyncWorkerGroup? workerGroup,
        RadarProcessingArchiveProviderMode providerMode,
        int queueCapacity,
        TimeSpan? queueTimeout,
        RadarProcessingQueuedProviderOverlapMode providerOverlapMode,
        RadarProcessingRetainedPayloadStrategy retentionStrategy,
        long? queueRetainedPayloadBytes,
        TimeSpan overlapConsumerDelay,
        RadarProcessingRetainedPayloadFactory? retainedPayloadFactory,
        CancellationToken cancellationToken)
    {
        using var processor = new ArchiveRebalanceBatchProcessor(
            sourceUniverse,
            mode,
            partitionCount,
            shardCount,
            hardeningOptions,
            pressureSkewOptions,
            executionMode,
            asyncExecution,
            workerTelemetryRecorder,
            workerGroup);
        var totals = CacheIterationTotals.Empty;
        var queueTelemetry = RadarProcessingProviderQueueTelemetrySummary.Empty;
        var retentionTelemetry = providerMode == RadarProcessingArchiveProviderMode.QueuedOwned
            ? new RadarProcessingRetainedPayloadTelemetrySummary(retentionStrategy)
            : RadarProcessingRetainedPayloadTelemetrySummary.Empty;
        var overlapTelemetry = providerOverlapMode == RadarProcessingQueuedProviderOverlapMode.ProducerConsumer
            ? new RadarProcessingArchiveOverlapTelemetrySummary(retentionStrategy)
            : RadarProcessingArchiveOverlapTelemetrySummary.Empty;

        if (providerMode == RadarProcessingArchiveProviderMode.QueuedOwned &&
            providerOverlapMode == RadarProcessingQueuedProviderOverlapMode.ProducerConsumer)
        {
            var queuedResult = PublishCacheQueuedOwnedOverlap(
                archiveSession,
                directoryInfo,
                date,
                radarId,
                maxFiles,
                processor,
                queueCapacity,
                queueTimeout,
                retentionStrategy,
                queueRetainedPayloadBytes,
                overlapConsumerDelay,
                retainedPayloadFactory,
                cancellationToken);

            return processor
                .BuildTelemetry(queuedResult.Totals)
                .WithQueueTelemetry(queuedResult.QueueTelemetry)
                .WithRetentionTelemetry(queuedResult.RetentionTelemetry)
                .WithOverlapTelemetry(queuedResult.OverlapTelemetry);
        }

        foreach (var fileInfo in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).OrderBy(file => file.FullName))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (totals.ExaminedFileCount >= maxFiles)
            {
                break;
            }

            if (!MatchesRadar(fileInfo, radarId) ||
                !MatchesDate(fileInfo, date))
            {
                continue;
            }

            totals.ExaminedFileCount++;
            if (!ArchiveTwoFileReader.IsArchiveTwoBaseData(fileInfo))
            {
                totals.SkippedFileCount++;
                continue;
            }

            ArchiveRadarEventBatchPublishResult publishResult;
            if (providerMode == RadarProcessingArchiveProviderMode.BlockingBorrowed)
            {
                publishResult = archiveSession.PublishFile(fileInfo.FullName, processor, cancellationToken);
            }
            else
            {
                var queuedResult = providerOverlapMode == RadarProcessingQueuedProviderOverlapMode.ProducerConsumer
                    ? PublishFileQueuedOwnedOverlap(
                        archiveSession,
                        fileInfo.FullName,
                        processor,
                        queueCapacity,
                        queueTimeout,
                        retentionStrategy,
                        queueRetainedPayloadBytes,
                        overlapConsumerDelay,
                        retainedPayloadFactory,
                        cancellationToken)
                    : PublishFileQueuedOwned(
                        archiveSession,
                        fileInfo.FullName,
                        processor,
                        queueCapacity,
                        queueTimeout,
                        retentionStrategy,
                        queueRetainedPayloadBytes,
                        retainedPayloadFactory,
                        cancellationToken);

                publishResult = queuedResult.PublishResult;
                queueTelemetry = AddQueueTelemetry(queueTelemetry, queuedResult.QueueTelemetry);
                retentionTelemetry = AddRetentionTelemetry(retentionTelemetry, queuedResult.RetentionTelemetry);
                overlapTelemetry = AddOverlapTelemetry(overlapTelemetry, queuedResult.OverlapTelemetry);
            }

            totals.Add(publishResult);
        }

        return processor
            .BuildTelemetry(totals)
            .WithQueueTelemetry(queueTelemetry)
            .WithRetentionTelemetry(retentionTelemetry)
            .WithOverlapTelemetry(overlapTelemetry);
    }

    private static QueuedArchivePublishResult PublishFileQueuedOwned(
        NexradArchiveRadarEventBatchPublishSession archiveSession,
        string filePath,
        ArchiveRebalanceBatchProcessor processor,
        int queueCapacity,
        TimeSpan? queueTimeout,
        RadarProcessingRetainedPayloadStrategy retentionStrategy,
        long? queueRetainedPayloadBytes,
        RadarProcessingRetainedPayloadFactory? retainedPayloadFactory,
        CancellationToken cancellationToken)
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(
                capacity: queueCapacity,
                enqueueTimeout: queueTimeout,
                maxRetainedPayloadBytes: queueRetainedPayloadBytes));
        using var queueingPublisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(
            queue,
            retainedPayloadOptions: new RadarProcessingRetainedPayloadOptions(
                retentionStrategy,
                queueRetainedPayloadBytes),
            retainedPayloadFactory: retainedPayloadFactory);

        var publishResult = archiveSession.PublishFile(filePath, queueingPublisher, cancellationToken);
        queueingPublisher.CompleteAdding();

        var drainStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        var completed = 0L;
        var failed = 0L;
        var canceled = 0L;
        while (true)
        {
            var dequeue = queue.DequeueAsync(cancellationToken).AsTask().GetAwaiter().GetResult();
            switch (dequeue.Status)
            {
                case RadarProcessingOwnedBatchDequeueStatus.Item:
                    try
                    {
                        var queuedBatch = dequeue.Batch!;
                        using var consumerResourceLease = queueingPublisher.AcquireConsumerResourceLease(queuedBatch.Sequence);
                        processor.Publish(queuedBatch.Batch, cancellationToken);
                        completed++;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        canceled++;
                        throw;
                    }
                    catch
                    {
                        failed++;
                        throw;
                    }

                    break;

                case RadarProcessingOwnedBatchDequeueStatus.Closed:
                    var providerResult = queueingPublisher.CreateResult();
                    var queueTelemetry = WithQueueCompletionTelemetry(
                            queue.CreateTelemetrySummary(),
                            completed,
                            failed,
                            canceled,
                            skippedAfterFault: 0,
                            System.Diagnostics.Stopwatch.GetElapsedTime(drainStarted))
                        .WithRetainedResourcePressure(providerResult.Telemetry.RetainedResourcePressure);
                    return new QueuedArchivePublishResult(
                        publishResult,
                        queueTelemetry,
                        providerResult.RetentionTelemetry,
                        RadarProcessingArchiveOverlapTelemetrySummary.Empty);

                case RadarProcessingOwnedBatchDequeueStatus.Canceled:
                    throw new OperationCanceledException(cancellationToken);

                case RadarProcessingOwnedBatchDequeueStatus.Faulted:
                    throw new InvalidOperationException(dequeue.Message);

                case RadarProcessingOwnedBatchDequeueStatus.Disposed:
                    throw new ObjectDisposedException(nameof(RadarProcessingOwnedBatchQueue));

                default:
                    RadarProcessingOwnedBatchDequeueResult.EnsureKnownStatus(dequeue.Status);
                    throw new ArgumentOutOfRangeException(nameof(dequeue));
            }
        }
    }

    private static QueuedArchivePublishResult PublishFileQueuedOwnedOverlap(
        NexradArchiveRadarEventBatchPublishSession archiveSession,
        string filePath,
        ArchiveRebalanceBatchProcessor processor,
        int queueCapacity,
        TimeSpan? queueTimeout,
        RadarProcessingRetainedPayloadStrategy retentionStrategy,
        long? queueRetainedPayloadBytes,
        TimeSpan overlapConsumerDelay,
        RadarProcessingRetainedPayloadFactory? retainedPayloadFactory,
        CancellationToken cancellationToken)
    {
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var result = runner.RunAsync(
                (publisher, token) => archiveSession.PublishFile(filePath, publisher, token),
                (queue, publisher, token) => DrainQueueToProcessorAsync(
                    queue,
                    publisher,
                    processor,
                    overlapConsumerDelay,
                    token),
                new RadarProcessingArchiveQueuedOverlapOptions(
                    new RadarProcessingProviderQueueOptions(
                        capacity: queueCapacity,
                        enqueueTimeout: queueTimeout,
                        maxRetainedPayloadBytes: queueRetainedPayloadBytes),
                    new RadarProcessingRetainedPayloadOptions(
                        retentionStrategy,
                        queueRetainedPayloadBytes),
                    retainedPayloadFactory),
                cancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        if (!result.IsCompleted)
        {
            throw new InvalidOperationException(result.Message);
        }

        return new QueuedArchivePublishResult(
            result.Producer.PublishResult!,
            result.QueueTelemetry,
            result.OverlapTelemetry.RetentionTelemetry,
            result.OverlapTelemetry);
    }

    private static async ValueTask<RadarProcessingQueuedSessionResult> DrainQueueToProcessorAsync(
        RadarProcessingOwnedBatchQueue queue,
        ArchiveOwnedRadarEventBatchQueueingPublisher publisher,
        ArchiveRebalanceBatchProcessor processor,
        TimeSpan overlapConsumerDelay,
        CancellationToken cancellationToken)
    {
        var drainStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        var completed = 0L;
        var failed = 0L;
        var canceled = 0L;
        while (true)
        {
            var dequeue = await queue.DequeueAsync(cancellationToken).ConfigureAwait(false);
            switch (dequeue.Status)
            {
                case RadarProcessingOwnedBatchDequeueStatus.Item:
                    var queuedBatch = dequeue.Batch!;
                    try
                    {
                        using var consumerResourceLease = publisher.AcquireConsumerResourceLease(queuedBatch.Sequence);
                        if (overlapConsumerDelay > TimeSpan.Zero)
                        {
                            await Task.Delay(overlapConsumerDelay, cancellationToken).ConfigureAwait(false);
                        }

                        processor.Publish(queuedBatch.Batch, cancellationToken);
                        completed++;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        canceled++;
                        throw;
                    }
                    catch
                    {
                        failed++;
                        throw;
                    }

                    break;

                case RadarProcessingOwnedBatchDequeueStatus.Closed:
                    var queueTelemetry = WithQueueCompletionTelemetry(
                        queue.CreateTelemetrySummary(),
                        completed,
                        failed,
                        canceled,
                        skippedAfterFault: 0,
                        System.Diagnostics.Stopwatch.GetElapsedTime(drainStarted));
                    return new RadarProcessingQueuedSessionResult(
                        RadarProcessingQueuedSessionStatus.Completed,
                        queueTelemetry);

                case RadarProcessingOwnedBatchDequeueStatus.Canceled:
                    throw new OperationCanceledException(cancellationToken);

                case RadarProcessingOwnedBatchDequeueStatus.Faulted:
                    throw new InvalidOperationException(dequeue.Message);

                case RadarProcessingOwnedBatchDequeueStatus.Disposed:
                    throw new ObjectDisposedException(nameof(RadarProcessingOwnedBatchQueue));

                default:
                    RadarProcessingOwnedBatchDequeueResult.EnsureKnownStatus(dequeue.Status);
                    throw new ArgumentOutOfRangeException(nameof(dequeue));
            }
        }
    }

    private static QueuedArchiveCachePublishResult PublishCacheQueuedOwnedOverlap(
        NexradArchiveRadarEventBatchPublishSession archiveSession,
        DirectoryInfo directoryInfo,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        ArchiveRebalanceBatchProcessor processor,
        int queueCapacity,
        TimeSpan? queueTimeout,
        RadarProcessingRetainedPayloadStrategy retentionStrategy,
        long? queueRetainedPayloadBytes,
        TimeSpan overlapConsumerDelay,
        RadarProcessingRetainedPayloadFactory? retainedPayloadFactory,
        CancellationToken cancellationToken)
    {
        var selection = SelectCacheArchiveFiles(directoryInfo, date, radarId, maxFiles, cancellationToken);
        if (selection.BaseDataFiles.Count == 0)
        {
            return new QueuedArchiveCachePublishResult(
                selection.Totals,
                RadarProcessingProviderQueueTelemetrySummary.Empty,
                new RadarProcessingRetainedPayloadTelemetrySummary(retentionStrategy),
                new RadarProcessingArchiveOverlapTelemetrySummary(retentionStrategy));
        }

        var publishedTotals = selection.Totals;
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var result = runner.RunAsync(
                (publisher, token) =>
                {
                    var totals = selection.Totals;
                    ArchiveRadarEventBatchPublishResult? lastPublishResult = null;
                    foreach (var fileInfo in selection.BaseDataFiles)
                    {
                        token.ThrowIfCancellationRequested();
                        var publishResult = archiveSession.PublishFile(fileInfo.FullName, publisher, token);
                        totals.Add(publishResult);
                        lastPublishResult = publishResult;
                    }

                    publishedTotals = totals;
                    return CreateCacheAggregatePublishResult(
                        directoryInfo.FullName,
                        totals,
                        lastPublishResult ?? throw new InvalidOperationException("Cache overlap producer did not publish any archive files."));
                },
                (queue, publisher, token) => DrainQueueToProcessorAsync(
                    queue,
                    publisher,
                    processor,
                    overlapConsumerDelay,
                    token),
                new RadarProcessingArchiveQueuedOverlapOptions(
                    new RadarProcessingProviderQueueOptions(
                        capacity: queueCapacity,
                        enqueueTimeout: queueTimeout,
                        maxRetainedPayloadBytes: queueRetainedPayloadBytes),
                    new RadarProcessingRetainedPayloadOptions(
                        retentionStrategy,
                        queueRetainedPayloadBytes),
                    retainedPayloadFactory),
                cancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        if (!result.IsCompleted)
        {
            throw new InvalidOperationException(result.Message);
        }

        return new QueuedArchiveCachePublishResult(
            publishedTotals,
            result.QueueTelemetry,
            result.OverlapTelemetry.RetentionTelemetry,
            result.OverlapTelemetry);
    }

    private static CacheArchiveFileSelection SelectCacheArchiveFiles(
        DirectoryInfo directoryInfo,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        CancellationToken cancellationToken)
    {
        var totals = CacheIterationTotals.Empty;
        var baseDataFiles = new List<FileInfo>();
        foreach (var fileInfo in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).OrderBy(file => file.FullName))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (totals.ExaminedFileCount >= maxFiles)
            {
                break;
            }

            if (!MatchesRadar(fileInfo, radarId) ||
                !MatchesDate(fileInfo, date))
            {
                continue;
            }

            totals.ExaminedFileCount++;
            if (!ArchiveTwoFileReader.IsArchiveTwoBaseData(fileInfo))
            {
                totals.SkippedFileCount++;
                continue;
            }

            baseDataFiles.Add(fileInfo);
        }

        return new CacheArchiveFileSelection(totals, baseDataFiles);
    }

    private static ArchiveRadarEventBatchPublishResult CreateCacheAggregatePublishResult(
        string cachePath,
        CacheIterationTotals totals,
        ArchiveRadarEventBatchPublishResult lastPublishResult) =>
        new(
            cachePath,
            lastPublishResult.Decompressor,
            lastPublishResult.DegreeOfParallelism,
            totals.FileSizeBytes,
            checked((int)totals.CompressedRecordCount),
            totals.CompressedBytes,
            totals.DecompressedBytes,
            lastPublishResult.StreamSchemaVersion,
            lastPublishResult.DictionaryVersion,
            lastPublishResult.SourceUniverseVersion,
            totals.BatchCount,
            totals.EventCount,
            totals.PayloadBytes,
            totals.PayloadValueCount,
            totals.RawValueChecksum,
            lastPublishResult.DictionarySnapshot);

    private static RadarProcessingProviderQueueTelemetrySummary WithQueueCompletionTelemetry(
        RadarProcessingProviderQueueTelemetrySummary queueTelemetry,
        long completed,
        long failed,
        long canceled,
        long skippedAfterFault,
        TimeSpan drainTime)
    {
        ArgumentNullException.ThrowIfNull(queueTelemetry);
        ArgumentOutOfRangeException.ThrowIfNegative(completed);
        ArgumentOutOfRangeException.ThrowIfNegative(failed);
        ArgumentOutOfRangeException.ThrowIfNegative(canceled);
        ArgumentOutOfRangeException.ThrowIfNegative(skippedAfterFault);
        if (drainTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(drainTime));
        }

        return new RadarProcessingProviderQueueTelemetrySummary(
            queueTelemetry.OwnedSnapshotCount,
            queueTelemetry.OwnedSnapshotPayloadBytes,
            queueTelemetry.OwnedSnapshotAllocatedBytes,
            queueTelemetry.TotalOwnedSnapshotTime,
            queueTelemetry.EnqueueAttemptCount,
            queueTelemetry.EnqueuedBatchCount,
            queueTelemetry.EnqueueFullCount,
            queueTelemetry.EnqueueTimedOutCount,
            queueTelemetry.EnqueueCanceledCount,
            queueTelemetry.EnqueueClosedCount,
            queueTelemetry.EnqueueFaultedCount,
            queueTelemetry.TotalEnqueueWaitTime,
            queueTelemetry.DequeuedBatchCount,
            completed,
            failed,
            canceled,
            skippedAfterFault,
            queueTelemetry.TotalDrainTime + drainTime,
            queueTelemetry.QueueDepthHighWatermark,
            queueTelemetry.QueuedPayloadBytesHighWatermark,
            queueTelemetry.OwnedSnapshotPayloadValueCount,
            queueTelemetry.TotalProviderToProcessingLatency,
            queueTelemetry.RecentDetails,
            queueTelemetry.DroppedRecentDetailCount,
            queueTelemetry.OwnedSnapshotEventCount,
            queueTelemetry.TotalDequeueWaitTime,
            queueTelemetry.RetainedResourcePressure);
    }

    private static RadarProcessingProviderQueueTelemetrySummary AddQueueTelemetry(
        RadarProcessingProviderQueueTelemetrySummary current,
        RadarProcessingProviderQueueTelemetrySummary next)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(next);

        var recentDetails = CreateBoundedRecentDetails(
            current.RecentDetails,
            next.RecentDetails,
            out var droppedRecentDetails);

        return new RadarProcessingProviderQueueTelemetrySummary(
            checked(current.OwnedSnapshotCount + next.OwnedSnapshotCount),
            checked(current.OwnedSnapshotPayloadBytes + next.OwnedSnapshotPayloadBytes),
            checked(current.OwnedSnapshotAllocatedBytes + next.OwnedSnapshotAllocatedBytes),
            current.TotalOwnedSnapshotTime + next.TotalOwnedSnapshotTime,
            checked(current.EnqueueAttemptCount + next.EnqueueAttemptCount),
            checked(current.EnqueuedBatchCount + next.EnqueuedBatchCount),
            checked(current.EnqueueFullCount + next.EnqueueFullCount),
            checked(current.EnqueueTimedOutCount + next.EnqueueTimedOutCount),
            checked(current.EnqueueCanceledCount + next.EnqueueCanceledCount),
            checked(current.EnqueueClosedCount + next.EnqueueClosedCount),
            checked(current.EnqueueFaultedCount + next.EnqueueFaultedCount),
            current.TotalEnqueueWaitTime + next.TotalEnqueueWaitTime,
            checked(current.DequeuedBatchCount + next.DequeuedBatchCount),
            checked(current.CompletedBatchCount + next.CompletedBatchCount),
            checked(current.FailedBatchCount + next.FailedBatchCount),
            checked(current.CanceledBatchCount + next.CanceledBatchCount),
            checked(current.SkippedAfterFaultCount + next.SkippedAfterFaultCount),
            current.TotalDrainTime + next.TotalDrainTime,
            Math.Max(current.QueueDepthHighWatermark, next.QueueDepthHighWatermark),
            Math.Max(current.QueuedPayloadBytesHighWatermark, next.QueuedPayloadBytesHighWatermark),
            checked(current.OwnedSnapshotPayloadValueCount + next.OwnedSnapshotPayloadValueCount),
            current.TotalProviderToProcessingLatency + next.TotalProviderToProcessingLatency,
            recentDetails,
            checked(current.DroppedRecentDetailCount + next.DroppedRecentDetailCount + droppedRecentDetails),
            checked(current.OwnedSnapshotEventCount + next.OwnedSnapshotEventCount),
            current.TotalDequeueWaitTime + next.TotalDequeueWaitTime,
            AddRetainedResourcePressure(current.RetainedResourcePressure, next.RetainedResourcePressure));
    }

    private static RadarProcessingRetainedResourcePressureSummary AddRetainedResourcePressure(
        RadarProcessingRetainedResourcePressureSummary current,
        RadarProcessingRetainedResourcePressureSummary next)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(next);

        var currentPendingBatchCount = checked(
            current.CurrentPendingRetainedBatchCount +
            next.CurrentPendingRetainedBatchCount);
        var currentPendingPayloadBytes = checked(
            current.CurrentPendingRetainedPayloadBytes +
            next.CurrentPendingRetainedPayloadBytes);
        var currentActiveBatchCount = checked(
            current.CurrentActiveRetainedBatchCount +
            next.CurrentActiveRetainedBatchCount);
        var currentActivePayloadBytes = checked(
            current.CurrentActiveRetainedPayloadBytes +
            next.CurrentActiveRetainedPayloadBytes);
        var pendingBatchHighWatermark = Math.Max(
            Math.Max(current.PendingRetainedBatchCountHighWatermark, next.PendingRetainedBatchCountHighWatermark),
            currentPendingBatchCount);
        var pendingPayloadHighWatermark = Math.Max(
            Math.Max(current.PendingRetainedPayloadBytesHighWatermark, next.PendingRetainedPayloadBytesHighWatermark),
            currentPendingPayloadBytes);
        var activeBatchHighWatermark = Math.Max(
            Math.Max(current.ActiveRetainedBatchCountHighWatermark, next.ActiveRetainedBatchCountHighWatermark),
            currentActiveBatchCount);
        var activePayloadHighWatermark = Math.Max(
            Math.Max(current.ActiveRetainedPayloadBytesHighWatermark, next.ActiveRetainedPayloadBytesHighWatermark),
            currentActivePayloadBytes);
        var currentCombinedBatchCount = checked(currentPendingBatchCount + currentActiveBatchCount);
        var currentCombinedPayloadBytes = checked(currentPendingPayloadBytes + currentActivePayloadBytes);

        return new RadarProcessingRetainedResourcePressureSummary(
            currentPendingBatchCount,
            currentPendingPayloadBytes,
            pendingBatchHighWatermark,
            pendingPayloadHighWatermark,
            currentActiveBatchCount,
            currentActivePayloadBytes,
            activeBatchHighWatermark,
            activePayloadHighWatermark,
            Math.Max(
                Math.Max(
                    current.CombinedRetainedBatchCountHighWatermark,
                    next.CombinedRetainedBatchCountHighWatermark),
                currentCombinedBatchCount),
            Math.Max(
                Math.Max(
                    current.CombinedRetainedPayloadBytesHighWatermark,
                    next.CombinedRetainedPayloadBytesHighWatermark),
                currentCombinedPayloadBytes));
    }

    private static RadarProcessingRetainedPayloadTelemetrySummary AddRetentionTelemetry(
        RadarProcessingRetainedPayloadTelemetrySummary current,
        RadarProcessingRetainedPayloadTelemetrySummary next)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(next);

        var strategy = next.RetentionAttemptCount > 0 || current.RetentionAttemptCount == 0
            ? next.Strategy
            : current.Strategy;
        if (current.RetentionAttemptCount > 0 &&
            next.RetentionAttemptCount > 0 &&
            current.Strategy != next.Strategy)
        {
            throw new InvalidOperationException("Cannot aggregate retained payload telemetry from different strategies.");
        }

        return new RadarProcessingRetainedPayloadTelemetrySummary(
            strategy,
            checked(current.RetentionAttemptCount + next.RetentionAttemptCount),
            checked(current.RetainedBatchCount + next.RetainedBatchCount),
            checked(current.RetentionUnsupportedStrategyCount + next.RetentionUnsupportedStrategyCount),
            checked(current.RetentionFailedCopyCount + next.RetentionFailedCopyCount),
            checked(current.RetentionCanceledCount + next.RetentionCanceledCount),
            checked(current.RetentionInvalidInputCount + next.RetentionInvalidInputCount),
            checked(current.RetainedEventCount + next.RetainedEventCount),
            checked(current.RetainedPayloadBytes + next.RetainedPayloadBytes),
            checked(current.RetainedPayloadValueCount + next.RetainedPayloadValueCount),
            checked(current.AllocatedBytes + next.AllocatedBytes),
            current.TotalRetentionTime + next.TotalRetentionTime,
            checked(current.TransferCount + next.TransferCount),
            checked(current.PoolRentCount + next.PoolRentCount),
            checked(current.PoolReturnCount + next.PoolReturnCount),
            checked(current.PoolMissCount + next.PoolMissCount),
            checked(current.ReleaseAttemptCount + next.ReleaseAttemptCount),
            checked(current.ReleasedBatchCount + next.ReleasedBatchCount),
            checked(current.AlreadyReleasedBatchCount + next.AlreadyReleasedBatchCount),
            checked(current.ReleaseFailedCount + next.ReleaseFailedCount),
            checked(current.ReleaseNotRequiredCount + next.ReleaseNotRequiredCount),
            current.TotalReleaseTime + next.TotalReleaseTime,
            eventPoolRentCount: checked(current.EventPoolRentCount + next.EventPoolRentCount),
            eventPoolReturnCount: checked(current.EventPoolReturnCount + next.EventPoolReturnCount),
            eventPoolMissCount: checked(current.EventPoolMissCount + next.EventPoolMissCount),
            payloadPoolRentCount: checked(current.PayloadPoolRentCount + next.PayloadPoolRentCount),
            payloadPoolReturnCount: checked(current.PayloadPoolReturnCount + next.PayloadPoolReturnCount),
            payloadPoolMissCount: checked(current.PayloadPoolMissCount + next.PayloadPoolMissCount));
    }

    private static RadarProcessingArchiveOverlapTelemetrySummary AddOverlapTelemetry(
        RadarProcessingArchiveOverlapTelemetrySummary current,
        RadarProcessingArchiveOverlapTelemetrySummary next)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(next);

        var hasCurrent = current.Elapsed > TimeSpan.Zero ||
            current.ProducerActiveTime > TimeSpan.Zero ||
            current.ConsumerActiveTime > TimeSpan.Zero ||
            current.OverlapElapsed > TimeSpan.Zero;
        var hasNext = next.Elapsed > TimeSpan.Zero ||
            next.ProducerActiveTime > TimeSpan.Zero ||
            next.ConsumerActiveTime > TimeSpan.Zero ||
            next.OverlapElapsed > TimeSpan.Zero;
        var strategy = hasNext || !hasCurrent
            ? next.RetentionStrategy
            : current.RetentionStrategy;
        if (hasCurrent &&
            hasNext &&
            current.RetentionStrategy != next.RetentionStrategy)
        {
            throw new InvalidOperationException("Cannot aggregate overlap telemetry from different retention strategies.");
        }

        return new RadarProcessingArchiveOverlapTelemetrySummary(
            strategy,
            current.Elapsed + next.Elapsed,
            current.ProducerActiveTime + next.ProducerActiveTime,
            current.ConsumerActiveTime + next.ConsumerActiveTime,
            current.OverlapElapsed + next.OverlapElapsed,
            checked(current.MeasuredAllocatedBytes + next.MeasuredAllocatedBytes),
            AddQueueTelemetry(current.QueueTelemetry, next.QueueTelemetry),
            AddRetentionTelemetry(current.RetentionTelemetry, next.RetentionTelemetry));
    }

    private static IReadOnlyCollection<RadarProcessingProviderQueueRecentDetail> CreateBoundedRecentDetails(
        IReadOnlyCollection<RadarProcessingProviderQueueRecentDetail> current,
        IReadOnlyCollection<RadarProcessingProviderQueueRecentDetail> next,
        out long droppedRecentDetails)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(next);

        var capacity = RadarProcessingProviderQueueOptions.Default.RecentDetailCapacity;
        var totalCount = checked(current.Count + next.Count);
        var skipCount = Math.Max(0, totalCount - capacity);
        droppedRecentDetails = skipCount;
        if (capacity == 0 || totalCount == 0)
        {
            return Array.Empty<RadarProcessingProviderQueueRecentDetail>();
        }

        var retainedCount = totalCount - skipCount;
        var result = new RadarProcessingProviderQueueRecentDetail[retainedCount];
        var writeIndex = 0;
        var readIndex = 0;

        CopyRecentDetails(current, skipCount, result, ref readIndex, ref writeIndex);
        CopyRecentDetails(next, skipCount, result, ref readIndex, ref writeIndex);

        return result;
    }

    private static void CopyRecentDetails(
        IReadOnlyCollection<RadarProcessingProviderQueueRecentDetail> source,
        int skipCount,
        RadarProcessingProviderQueueRecentDetail[] destination,
        ref int readIndex,
        ref int writeIndex)
    {
        foreach (var detail in source)
        {
            if (readIndex++ < skipCount)
            {
                continue;
            }

            destination[writeIndex++] = detail;
        }
    }

    private static bool MatchesRadar(FileInfo fileInfo, string? radarId)
    {
        if (radarId is null)
        {
            return true;
        }

        return fileInfo.Name.StartsWith(radarId, StringComparison.OrdinalIgnoreCase) ||
            fileInfo.DirectoryName?.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => string.Equals(segment, radarId, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static bool MatchesDate(FileInfo fileInfo, DateOnly? date)
    {
        if (date is null)
        {
            return true;
        }

        return TryReadDateFromFileName(fileInfo.Name, out var fileNameDate) && fileNameDate == date ||
            PathContainsDate(fileInfo.FullName, date.Value);
    }

    private static bool TryReadDateFromFileName(string fileName, out DateOnly date)
    {
        date = default;
        if (fileName.Length < 12)
        {
            return false;
        }

        var dateText = fileName.AsSpan(4, 8);
        if (!int.TryParse(dateText[..4], out var year) ||
            !int.TryParse(dateText.Slice(4, 2), out var month) ||
            !int.TryParse(dateText.Slice(6, 2), out var day))
        {
            return false;
        }

        try
        {
            date = new DateOnly(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool PathContainsDate(string path, DateOnly date)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var i = 0; i <= segments.Length - 3; i++)
        {
            if (string.Equals(segments[i], date.Year.ToString("0000"), StringComparison.Ordinal) &&
                string.Equals(segments[i + 1], date.Month.ToString("00"), StringComparison.Ordinal) &&
                string.Equals(segments[i + 2], date.Day.ToString("00"), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureKnownMode(RadarProcessingSyntheticRebalanceBenchmarkMode mode)
    {
        if (mode is not RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance and
            not RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly and
            not RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession)
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    private static void EnsureKnownExecutionMode(RadarProcessingExecutionMode executionMode)
    {
        if (executionMode is not RadarProcessingExecutionMode.PartitionedBarrier and
            not RadarProcessingExecutionMode.AsyncShardTransport)
        {
            throw new ArgumentOutOfRangeException(nameof(executionMode));
        }
    }

    private static void EnsureKnownProviderMode(RadarProcessingArchiveProviderMode providerMode)
    {
        if (providerMode is not RadarProcessingArchiveProviderMode.BlockingBorrowed and
            not RadarProcessingArchiveProviderMode.QueuedOwned)
        {
            throw new ArgumentOutOfRangeException(nameof(providerMode));
        }
    }

    private static void EnsureKnownProviderOverlapMode(RadarProcessingQueuedProviderOverlapMode providerOverlapMode)
    {
        if (providerOverlapMode is not RadarProcessingQueuedProviderOverlapMode.None and
            not RadarProcessingQueuedProviderOverlapMode.ProducerConsumer)
        {
            throw new ArgumentOutOfRangeException(nameof(providerOverlapMode));
        }
    }

    private static void ValidateQueueTimeout(TimeSpan? queueTimeout)
    {
        if (queueTimeout.HasValue &&
            queueTimeout.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(queueTimeout),
                queueTimeout,
                "Queue timeout must be positive when specified.");
        }
    }

    private static void ValidateQueueRetainedPayloadBytes(long? queueRetainedPayloadBytes)
    {
        if (queueRetainedPayloadBytes.HasValue &&
            queueRetainedPayloadBytes.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(queueRetainedPayloadBytes),
                queueRetainedPayloadBytes,
                "Queue retained payload byte capacity must be positive when specified.");
        }
    }

    private static void ValidateOverlapConsumerDelay(TimeSpan overlapConsumerDelay)
    {
        if (overlapConsumerDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(overlapConsumerDelay),
                overlapConsumerDelay,
                "Overlap consumer delay cannot be negative.");
        }
    }

    private static void ValidateQueuedProviderControls(
        RadarProcessingArchiveProviderMode providerMode,
        RadarProcessingQueuedProviderOverlapMode providerOverlapMode,
        RadarProcessingRetainedPayloadStrategy retentionStrategy,
        long? queueRetainedPayloadBytes,
        TimeSpan overlapConsumerDelay)
    {
        if (providerOverlapMode != RadarProcessingQueuedProviderOverlapMode.None &&
            providerMode != RadarProcessingArchiveProviderMode.QueuedOwned)
        {
            throw new InvalidOperationException("Provider overlap mode requires queued-owned archive provider mode.");
        }

        if (retentionStrategy != RadarProcessingRetainedPayloadStrategy.SnapshotCopy &&
            providerMode != RadarProcessingArchiveProviderMode.QueuedOwned)
        {
            throw new InvalidOperationException("Retained payload strategies require queued-owned archive provider mode.");
        }

        if (queueRetainedPayloadBytes.HasValue &&
            providerMode != RadarProcessingArchiveProviderMode.QueuedOwned)
        {
            throw new InvalidOperationException("Queue retained payload byte capacity requires queued-owned archive provider mode.");
        }

        if (overlapConsumerDelay > TimeSpan.Zero &&
            (providerMode != RadarProcessingArchiveProviderMode.QueuedOwned ||
             providerOverlapMode != RadarProcessingQueuedProviderOverlapMode.ProducerConsumer))
        {
            throw new InvalidOperationException(
                "Overlap consumer delay requires queued-owned producer-consumer archive provider overlap.");
        }

        if (providerMode == RadarProcessingArchiveProviderMode.QueuedOwned &&
            retentionStrategy == RadarProcessingRetainedPayloadStrategy.BuilderTransfer)
        {
            throw new NotSupportedException("Builder-transfer retained payload strategy is not implemented for archive benchmarks.");
        }
    }

    private static void ValidateWorkerTelemetry(
        RadarProcessingWorkerTelemetrySummary? workerTelemetry,
        RadarProcessingWorkerTelemetryRecorder? workerTelemetryRecorder,
        RadarProcessingRebalanceHardeningOptions hardeningOptions)
    {
        if (workerTelemetry is null)
        {
            return;
        }

        var retentionValidation = RadarProcessingAsyncValidator.ValidateWorkerTelemetryRetention(
            workerTelemetry,
            workerTelemetryRecorder!.Options,
            hardeningOptions.ValidationProfile);
        if (!retentionValidation.IsValid)
        {
            throw new InvalidDataException(retentionValidation.Message);
        }
    }

    private static ulong AppendByte(ulong checksum, byte value) =>
        unchecked((checksum ^ value) * ChecksumPrime);

    private static ulong AppendInt32(ulong checksum, int value) =>
        AppendUInt32(checksum, unchecked((uint)value));

    private static ulong AppendUInt32(ulong checksum, uint value)
    {
        checksum = AppendByte(checksum, (byte)value);
        checksum = AppendByte(checksum, (byte)(value >> 8));
        checksum = AppendByte(checksum, (byte)(value >> 16));
        return AppendByte(checksum, (byte)(value >> 24));
    }

    private static ulong AppendInt64(ulong checksum, long value) =>
        AppendUInt64(checksum, unchecked((ulong)value));

    private static ulong AppendUInt64(ulong checksum, ulong value)
    {
        checksum = AppendByte(checksum, (byte)value);
        checksum = AppendByte(checksum, (byte)(value >> 8));
        checksum = AppendByte(checksum, (byte)(value >> 16));
        checksum = AppendByte(checksum, (byte)(value >> 24));
        checksum = AppendByte(checksum, (byte)(value >> 32));
        checksum = AppendByte(checksum, (byte)(value >> 40));
        checksum = AppendByte(checksum, (byte)(value >> 48));
        return AppendByte(checksum, (byte)(value >> 56));
    }

    private static IReadOnlyList<T> CreateReadOnlyList<T>(List<T>? values) =>
        values is { Count: > 0 }
            ? Array.AsReadOnly(values.ToArray())
            : Array.Empty<T>();

    private static IReadOnlyList<RadarProcessingRebalanceSkippedReasonCounter> CreateSortedSkippedReasonCounters(
        List<RadarProcessingRebalanceSkippedReasonCounter>? values)
    {
        if (values is not { Count: > 0 })
        {
            return Array.Empty<RadarProcessingRebalanceSkippedReasonCounter>();
        }

        var result = values.ToArray();
        Array.Sort(result, (left, right) => left.Reason.CompareTo(right.Reason));
        return Array.AsReadOnly(result);
    }

    private sealed class ArchiveRebalanceBatchProcessor : IArchiveRadarEventBatchPublisher, IDisposable
    {
        private readonly RadarProcessingSyntheticRebalanceBenchmarkMode mode;
        private readonly RadarProcessingCore? core;
        private readonly RadarProcessingAsyncCoreSession? asyncCoreSession;
        private readonly RadarProcessingPressureWindow? pressureWindow;
        private readonly RadarProcessingRebalanceSession? rebalanceSession;
        private readonly RadarProcessingAsyncRebalanceSession? asyncRebalanceSession;
        private readonly RadarProcessingPressureSkewTransformer? pressureSkewTransformer;
        private readonly System.Diagnostics.Stopwatch processingStopwatch = new();
        private ArchiveIterationTelemetry telemetry = ArchiveIterationTelemetry.Empty;
        private long processingCallbackAllocatedBytes;
        private bool disposed;

        public ArchiveRebalanceBatchProcessor(
            RadarSourceUniverse sourceUniverse,
            RadarProcessingSyntheticRebalanceBenchmarkMode mode,
            int partitionCount,
            int shardCount,
            RadarProcessingRebalanceHardeningOptions hardeningOptions,
            RadarProcessingPressureSkewOptions pressureSkewOptions,
            RadarProcessingExecutionMode executionMode,
            RadarProcessingAsyncExecutionOptions? asyncExecution,
            RadarProcessingWorkerTelemetryRecorder? workerTelemetryRecorder,
            RadarProcessingAsyncWorkerGroup? workerGroup)
        {
            ArgumentNullException.ThrowIfNull(hardeningOptions);
            ArgumentNullException.ThrowIfNull(pressureSkewOptions);

            this.mode = mode;
            pressureSkewTransformer = pressureSkewOptions.IsEnabled
                ? new RadarProcessingPressureSkewTransformer(pressureSkewOptions)
                : null;
            var coreOptions = new RadarProcessingCoreOptions(
                executionMode,
                partitionCount,
                shardCount,
                asyncExecution: asyncExecution);

            switch (mode)
            {
                case RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance:
                    core = new RadarProcessingCore(sourceUniverse, coreOptions);
                    asyncCoreSession = executionMode == RadarProcessingExecutionMode.AsyncShardTransport
                        ? CreateAsyncCoreSession(core, workerTelemetryRecorder, workerGroup)
                        : null;
                    break;
                case RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly:
                    core = new RadarProcessingCore(sourceUniverse, coreOptions);
                    asyncCoreSession = executionMode == RadarProcessingExecutionMode.AsyncShardTransport
                        ? CreateAsyncCoreSession(core, workerTelemetryRecorder, workerGroup)
                        : null;
                    pressureWindow = new RadarProcessingPressureWindow(
                        new RadarProcessingPressureWindowOptions(
                            sampleCapacity: 8,
                            minimumSampleCount: 1));
                    break;
                case RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession:
                    var rebalanceCore = new RadarProcessingCore(sourceUniverse, coreOptions);
                    rebalanceSession = new RadarProcessingRebalanceSession(
                        rebalanceCore,
                        pressureWindow: new RadarProcessingPressureWindow(
                            new RadarProcessingPressureWindowOptions(
                                sampleCapacity: 8,
                                minimumSampleCount: 1)),
                        policyState: new RadarProcessingRebalancePolicyState(
                            partitionCount,
                            shardCount,
                            new RadarProcessingRebalanceOptions(
                                budgetWindowEvaluationCount: 8,
                                globalMoveBudgetPerWindow: 1,
                                sourceShardMoveBudgetPerWindow: 1,
                                targetShardReceiveBudgetPerWindow: 1,
                                minimumPartitionResidencyEvaluations: 0,
                                partitionMoveCooldownEvaluations: 4,
                                sourceShardMoveCooldownEvaluations: 1,
                                targetShardReceiveCooldownEvaluations: 1)),
                        hardeningOptions: hardeningOptions,
                        pressureSkewOptions: pressureSkewOptions);
                    asyncRebalanceSession = executionMode == RadarProcessingExecutionMode.AsyncShardTransport
                        ? new RadarProcessingAsyncRebalanceSession(
                            rebalanceSession,
                            CreateAsyncCoreSession(rebalanceCore, workerTelemetryRecorder, workerGroup),
                            ownsAsyncCoreSession: true)
                        : null;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        public void Publish(RadarEventBatch batch, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var allocationBefore = RadarProcessingBenchmarkAllocationSnapshot.Capture();
            processingStopwatch.Start();
            try
            {
                telemetry = mode switch
                {
                    RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance =>
                        telemetry.Add(ProcessStatic(batch, cancellationToken)),
                    RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly =>
                        telemetry.Add(ProcessPressureSampling(batch, cancellationToken)),
                    RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession =>
                        telemetry.Add(ProcessRebalanceSession(batch, cancellationToken)),
                    _ => throw new ArgumentOutOfRangeException(nameof(mode))
                };
            }
            finally
            {
                processingStopwatch.Stop();
                processingCallbackAllocatedBytes = checked(
                    processingCallbackAllocatedBytes +
                    RadarProcessingBenchmarkAllocationSnapshot.Capture().DeltaSince(allocationBefore));
            }
        }

        public ArchiveIterationTelemetry BuildTelemetry(
            RadarPulse.Domain.Archive.ArchiveRadarEventBatchPublishResult publishResult) =>
            telemetry.WithPublishResult(
                publishResult,
                processingStopwatch.Elapsed,
                processingCallbackAllocatedBytes)
                .WithRetentionStats(CreateRetentionStats());

        public ArchiveIterationTelemetry BuildTelemetry(
            CacheIterationTotals totals) =>
            telemetry.WithPublishTotals(
                totals,
                processingStopwatch.Elapsed,
                processingCallbackAllocatedBytes)
                .WithRetentionStats(CreateRetentionStats());

        private ArchiveIterationTelemetry ProcessStatic(
            RadarEventBatch batch,
            CancellationToken cancellationToken)
        {
            var candidateCore = core ?? throw new InvalidOperationException("Static processing core was not initialized.");
            var result = asyncCoreSession is null
                ? candidateCore.Process(batch, cancellationToken)
                : asyncCoreSession.ProcessAsync(batch, cancellationToken).AsTask().GetAwaiter().GetResult();
            EnsureValidProcessingResult(result);
            return ArchiveIterationTelemetry.FromMetrics(
                candidateCore.CreateMetrics(),
                topologyVersionCount: 1);
        }

        private ArchiveIterationTelemetry ProcessPressureSampling(
            RadarEventBatch batch,
            CancellationToken cancellationToken)
        {
            var candidateCore = core ?? throw new InvalidOperationException("Pressure sampling core was not initialized.");
            var candidatePressureWindow = pressureWindow ??
                                          throw new InvalidOperationException("Pressure window was not initialized.");
            var result = asyncCoreSession is null
                ? candidateCore.Process(batch, cancellationToken)
                : asyncCoreSession.ProcessAsync(batch, cancellationToken).AsTask().GetAwaiter().GetResult();
            EnsureValidProcessingResult(result);
            var telemetryResult = result.Telemetry ??
                                  throw new InvalidDataException("Archive pressure sampling requires telemetry.");
            var pressureSample = RadarProcessingPressureSample.FromTelemetry(telemetryResult);
            var effectivePressureSample = pressureSkewTransformer?.Apply(
                pressureSample,
                telemetry.RebalanceEvaluationCount + 1,
                candidatePressureWindow.Options) ?? pressureSample;
            candidatePressureWindow.AddSample(effectivePressureSample);
            return ArchiveIterationTelemetry.FromMetrics(
                candidateCore.CreateMetrics(),
                topologyVersionCount: 1,
                rebalanceEvaluationCount: 1);
        }

        private ArchiveIterationTelemetry ProcessRebalanceSession(
            RadarEventBatch batch,
            CancellationToken cancellationToken)
        {
            var session = rebalanceSession ??
                          throw new InvalidOperationException("Rebalance session was not initialized.");
            var initialTopologyVersion = session.CurrentTopology.Version;
            var result = asyncRebalanceSession is null
                ? session.Process(batch, cancellationToken)
                : asyncRebalanceSession.ProcessAsync(batch, cancellationToken).AsTask().GetAwaiter().GetResult();
            var metrics = session.Core.CreateMetrics();
            return ArchiveIterationTelemetry.FromRebalanceSessionResult(result)
                .WithMetrics(
                    metrics,
                    session.CurrentTopology.Version.Value - initialTopologyVersion.Value + 1);
        }

        private static RadarProcessingAsyncCoreSession CreateAsyncCoreSession(
            RadarProcessingCore core,
            RadarProcessingWorkerTelemetryRecorder? workerTelemetryRecorder,
            RadarProcessingAsyncWorkerGroup? workerGroup) =>
            workerGroup is null
                ? new RadarProcessingAsyncCoreSession(core, workerTelemetryRecorder)
                : new RadarProcessingAsyncCoreSession(
                    core,
                    workerGroup,
                    workerTelemetryRecorder,
                    ownsWorkerGroup: false);

        private static void EnsureValidProcessingResult(RadarProcessingResult result)
        {
            if (!result.IsValid)
            {
                throw new InvalidDataException(result.Validation.Message);
            }

            if (result.Telemetry is null)
            {
                throw new InvalidDataException("Archive rebalance benchmark requires partitioned telemetry.");
            }

            if (result.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport)
            {
                var asyncValidation = RadarProcessingAsyncValidator.ValidateProcessingResult(
                    result,
                    RadarProcessingValidationProfile.Benchmark);
                if (!asyncValidation.IsValid)
                {
                    throw new InvalidDataException(asyncValidation.Message);
                }
            }
        }

        private RadarProcessingRebalanceRetentionStats CreateRetentionStats() =>
            rebalanceSession?.TelemetryRecorder.CreateSummary().RetentionStats ??
            new RadarProcessingRebalanceRetentionStats();

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (asyncRebalanceSession is not null)
            {
                asyncRebalanceSession.DisposeAsync().AsTask().GetAwaiter().GetResult();
                return;
            }

            if (asyncCoreSession is not null)
            {
                asyncCoreSession.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
    }

    private readonly record struct ArchiveIterationTelemetry(
        long ExaminedFileCount,
        long SkippedFileCount,
        long PublishedFileCount,
        long FileSizeBytes,
        long CompressedRecordCount,
        long CompressedBytes,
        long DecompressedBytes,
        long BatchCount,
        long EventCount,
        long PayloadBytes,
        long PayloadValueCount,
        long RawValueChecksum,
        long TopologyVersionCount,
        long RebalanceEvaluationCount,
        long AcceptedMoveCount,
        long SkippedDecisionCount,
        long DirectHotReliefCount,
        long ColdEvacuationCount,
        long FailedMigrationCount,
        bool ValidationSucceeded,
        ulong ValidationChecksum,
        List<RadarProcessingRebalanceSkippedReason>? SkippedReasons,
        List<RadarProcessingRebalanceSkippedReasonCounter>? SkippedReasonCounters,
        List<RadarProcessingSyntheticRebalanceMovePressure>? AcceptedMovePressures,
        RadarProcessingRebalanceRetentionStats RetentionStats,
        TimeSpan ProcessingElapsed,
        long ProcessingCallbackAllocatedBytes,
        RadarProcessingProviderQueueTelemetrySummary QueueTelemetry,
        RadarProcessingRetainedPayloadTelemetrySummary RetentionTelemetry,
        RadarProcessingArchiveOverlapTelemetrySummary OverlapTelemetry,
        long ProcessingValidationFailedBatchCount)
    {
        public static ArchiveIterationTelemetry Empty =>
            new(
                ExaminedFileCount: 0,
                SkippedFileCount: 0,
                PublishedFileCount: 0,
                FileSizeBytes: 0,
                CompressedRecordCount: 0,
                CompressedBytes: 0,
                DecompressedBytes: 0,
                BatchCount: 0,
                EventCount: 0,
                PayloadBytes: 0,
                PayloadValueCount: 0,
                RawValueChecksum: 0,
                TopologyVersionCount: 1,
                RebalanceEvaluationCount: 0,
                AcceptedMoveCount: 0,
                SkippedDecisionCount: 0,
                DirectHotReliefCount: 0,
                ColdEvacuationCount: 0,
                FailedMigrationCount: 0,
                ValidationSucceeded: true,
                ValidationChecksum: ChecksumInitial,
                SkippedReasons: null,
                SkippedReasonCounters: null,
                AcceptedMovePressures: null,
                RetentionStats: new RadarProcessingRebalanceRetentionStats(),
                ProcessingElapsed: TimeSpan.Zero,
                ProcessingCallbackAllocatedBytes: 0,
                QueueTelemetry: RadarProcessingProviderQueueTelemetrySummary.Empty,
                RetentionTelemetry: RadarProcessingRetainedPayloadTelemetrySummary.Empty,
                OverlapTelemetry: RadarProcessingArchiveOverlapTelemetrySummary.Empty,
                ProcessingValidationFailedBatchCount: 0);

        public static ArchiveIterationTelemetry FromMetrics(
            RadarProcessingMetrics metrics,
            long topologyVersionCount,
            long rebalanceEvaluationCount = 0) =>
            Empty.WithMetrics(metrics, topologyVersionCount) with
            {
                RebalanceEvaluationCount = rebalanceEvaluationCount,
                ValidationChecksum = ComputeChecksum(
                    metrics,
                    topologyVersionCount,
                    rebalanceEvaluationCount,
                    acceptedMoveCount: 0,
                    skippedDecisionCount: 0,
                    directHotReliefCount: 0,
                    coldEvacuationCount: 0,
                    failedMigrationCount: 0,
                    validationSucceeded: true)
            };

        public static ArchiveIterationTelemetry FromRebalanceSessionResult(
            RadarProcessingRebalanceSessionResult result)
        {
            List<RadarProcessingRebalanceSkippedReason>? skippedReasons = null;
            List<RadarProcessingRebalanceSkippedReasonCounter>? skippedReasonCounters = null;
            List<RadarProcessingSyntheticRebalanceMovePressure>? movePressures = null;
            var skippedDecisionCount = 0L;
            var acceptedMoveCount = 0L;
            var directHotReliefCount = 0L;
            var coldEvacuationCount = 0L;
            var failedMigrationCount = 0L;

            AddDecision(
                result.DirectHotReliefDecision,
                ref skippedReasons,
                ref skippedReasonCounters,
                ref skippedDecisionCount);
            AddDecision(
                result.ColdEvacuationDecision,
                ref skippedReasons,
                ref skippedReasonCounters,
                ref skippedDecisionCount);

            if (result.PublishedMigration)
            {
                acceptedMoveCount = 1;
                var decision = result.RebalanceDecision ??
                               throw new InvalidDataException("Published moves require a rebalance decision.");
                movePressures = new List<RadarProcessingSyntheticRebalanceMovePressure>(capacity: 1);
                movePressures.Add(CreateMovePressure(decision));
                if (decision.MoveKind == RadarProcessingRebalanceMoveKind.DirectHotRelief)
                {
                    directHotReliefCount = 1;
                }
                else if (decision.MoveKind == RadarProcessingRebalanceMoveKind.ColdEvacuation)
                {
                    coldEvacuationCount = 1;
                }
            }

            if (result.MigrationResult is not null && !result.MigrationResult.Succeeded)
            {
                failedMigrationCount = 1;
            }

            return Empty with
            {
                RebalanceEvaluationCount = result.EvaluatedRebalance ? 1 : 0,
                AcceptedMoveCount = acceptedMoveCount,
                SkippedDecisionCount = skippedDecisionCount,
                DirectHotReliefCount = directHotReliefCount,
                ColdEvacuationCount = coldEvacuationCount,
                FailedMigrationCount = failedMigrationCount,
                ValidationSucceeded = result.Validation.IsValid && result.ProcessingResult.IsValid,
                ProcessingValidationFailedBatchCount = result.ProcessingResult.IsValid ? 0 : 1,
                SkippedReasons = skippedReasons,
                SkippedReasonCounters = skippedReasonCounters,
                AcceptedMovePressures = movePressures
            };
        }

        public ArchiveIterationTelemetry Add(ArchiveIterationTelemetry other)
        {
            var skippedReasons = SkippedReasons;
            if (other.SkippedReasons is { Count: > 0 } otherSkippedReasons)
            {
                foreach (var reason in otherSkippedReasons)
                {
                    AddSkippedReason(ref skippedReasons, reason);
                }
            }

            var skippedReasonCounters = SkippedReasonCounters;
            if (other.SkippedReasonCounters is { Count: > 0 } otherSkippedReasonCounters)
            {
                foreach (var counter in otherSkippedReasonCounters)
                {
                    AddSkippedReasonCounter(ref skippedReasonCounters, counter.Reason, counter.Count);
                }
            }

            var movePressures = AcceptedMovePressures;
            if (other.AcceptedMovePressures is { Count: > 0 } otherMovePressures)
            {
                movePressures ??= new List<RadarProcessingSyntheticRebalanceMovePressure>(
                    otherMovePressures.Count);
                movePressures.AddRange(otherMovePressures);
            }

            return this with
            {
                ExaminedFileCount = checked(ExaminedFileCount + other.ExaminedFileCount),
                SkippedFileCount = checked(SkippedFileCount + other.SkippedFileCount),
                PublishedFileCount = checked(PublishedFileCount + other.PublishedFileCount),
                FileSizeBytes = FileSizeBytes == 0 ? other.FileSizeBytes : FileSizeBytes,
                CompressedRecordCount = checked(CompressedRecordCount + other.CompressedRecordCount),
                CompressedBytes = checked(CompressedBytes + other.CompressedBytes),
                DecompressedBytes = checked(DecompressedBytes + other.DecompressedBytes),
                BatchCount = checked(BatchCount + other.BatchCount),
                EventCount = checked(EventCount + other.EventCount),
                PayloadBytes = checked(PayloadBytes + other.PayloadBytes),
                PayloadValueCount = checked(PayloadValueCount + other.PayloadValueCount),
                RawValueChecksum = checked(RawValueChecksum + other.RawValueChecksum),
                TopologyVersionCount = Math.Max(TopologyVersionCount, other.TopologyVersionCount),
                RebalanceEvaluationCount = checked(RebalanceEvaluationCount + other.RebalanceEvaluationCount),
                AcceptedMoveCount = checked(AcceptedMoveCount + other.AcceptedMoveCount),
                SkippedDecisionCount = checked(SkippedDecisionCount + other.SkippedDecisionCount),
                DirectHotReliefCount = checked(DirectHotReliefCount + other.DirectHotReliefCount),
                ColdEvacuationCount = checked(ColdEvacuationCount + other.ColdEvacuationCount),
                FailedMigrationCount = checked(FailedMigrationCount + other.FailedMigrationCount),
                ValidationSucceeded = ValidationSucceeded && other.ValidationSucceeded,
                ValidationChecksum = AppendUInt64(ValidationChecksum, other.ValidationChecksum),
                ProcessingValidationFailedBatchCount = checked(
                    ProcessingValidationFailedBatchCount + other.ProcessingValidationFailedBatchCount),
                SkippedReasons = skippedReasons,
                SkippedReasonCounters = skippedReasonCounters,
                AcceptedMovePressures = movePressures,
                RetentionStats = AddRetentionStats(RetentionStats, other.RetentionStats),
                ProcessingElapsed = ProcessingElapsed + other.ProcessingElapsed,
                ProcessingCallbackAllocatedBytes = checked(
                    ProcessingCallbackAllocatedBytes + other.ProcessingCallbackAllocatedBytes),
                QueueTelemetry = AddQueueTelemetry(QueueTelemetry, other.QueueTelemetry),
                RetentionTelemetry = AddRetentionTelemetry(RetentionTelemetry, other.RetentionTelemetry),
                OverlapTelemetry = AddOverlapTelemetry(OverlapTelemetry, other.OverlapTelemetry)
            };
        }

        public ArchiveIterationTelemetry WithPublishResult(
            RadarPulse.Domain.Archive.ArchiveRadarEventBatchPublishResult result,
            TimeSpan processingElapsed,
            long processingCallbackAllocatedBytes) =>
            this with
            {
                ExaminedFileCount = 1,
                SkippedFileCount = 0,
                PublishedFileCount = result.BatchCount > 0 ? 1 : 0,
                FileSizeBytes = result.FileSizeBytes,
                CompressedRecordCount = result.CompressedRecordCount,
                CompressedBytes = result.CompressedBytes,
                DecompressedBytes = result.DecompressedBytes,
                BatchCount = result.BatchCount,
                EventCount = result.EventCount,
                PayloadBytes = result.PayloadBytes,
                PayloadValueCount = result.PayloadValueCount,
                RawValueChecksum = result.RawValueChecksum,
                ProcessingElapsed = processingElapsed,
                ProcessingCallbackAllocatedBytes = processingCallbackAllocatedBytes
            };

        public ArchiveIterationTelemetry WithQueueTelemetry(
            RadarProcessingProviderQueueTelemetrySummary queueTelemetry)
        {
            ArgumentNullException.ThrowIfNull(queueTelemetry);

            return this with
            {
                QueueTelemetry = queueTelemetry
            };
        }

        public ArchiveIterationTelemetry WithRetentionTelemetry(
            RadarProcessingRetainedPayloadTelemetrySummary retentionTelemetry)
        {
            ArgumentNullException.ThrowIfNull(retentionTelemetry);

            return this with
            {
                RetentionTelemetry = retentionTelemetry
            };
        }

        public ArchiveIterationTelemetry WithOverlapTelemetry(
            RadarProcessingArchiveOverlapTelemetrySummary overlapTelemetry)
        {
            ArgumentNullException.ThrowIfNull(overlapTelemetry);

            return this with
            {
                OverlapTelemetry = overlapTelemetry
            };
        }

        public ArchiveIterationTelemetry WithRetentionStats(
            RadarProcessingRebalanceRetentionStats retentionStats)
        {
            ArgumentNullException.ThrowIfNull(retentionStats);

            return this with
            {
                RetentionStats = retentionStats
            };
        }

        public ArchiveIterationTelemetry WithPublishTotals(
            CacheIterationTotals totals,
            TimeSpan processingElapsed,
            long processingCallbackAllocatedBytes) =>
            this with
            {
                ExaminedFileCount = totals.ExaminedFileCount,
                SkippedFileCount = totals.SkippedFileCount,
                PublishedFileCount = totals.PublishedFileCount,
                FileSizeBytes = totals.FileSizeBytes,
                CompressedRecordCount = totals.CompressedRecordCount,
                CompressedBytes = totals.CompressedBytes,
                DecompressedBytes = totals.DecompressedBytes,
                BatchCount = totals.BatchCount,
                EventCount = totals.EventCount,
                PayloadBytes = totals.PayloadBytes,
                PayloadValueCount = totals.PayloadValueCount,
                RawValueChecksum = totals.RawValueChecksum,
                ProcessingElapsed = processingElapsed,
                ProcessingCallbackAllocatedBytes = processingCallbackAllocatedBytes
            };

        public ArchiveIterationTelemetry WithMetrics(
            RadarProcessingMetrics metrics,
            long topologyVersionCount)
        {
            var validationChecksum = ComputeChecksum(
                metrics,
                topologyVersionCount,
                RebalanceEvaluationCount,
                AcceptedMoveCount,
                SkippedDecisionCount,
                DirectHotReliefCount,
                ColdEvacuationCount,
                FailedMigrationCount,
                ValidationSucceeded);

            return this with
            {
                TopologyVersionCount = topologyVersionCount,
                ValidationChecksum = validationChecksum
            };
        }

        public bool HasSameStableTotals(ArchiveIterationTelemetry other) =>
            ExaminedFileCount == other.ExaminedFileCount &&
            SkippedFileCount == other.SkippedFileCount &&
            PublishedFileCount == other.PublishedFileCount &&
            FileSizeBytes == other.FileSizeBytes &&
            CompressedRecordCount == other.CompressedRecordCount &&
            CompressedBytes == other.CompressedBytes &&
            DecompressedBytes == other.DecompressedBytes &&
            BatchCount == other.BatchCount &&
            EventCount == other.EventCount &&
            PayloadBytes == other.PayloadBytes &&
            PayloadValueCount == other.PayloadValueCount &&
            RawValueChecksum == other.RawValueChecksum &&
            TopologyVersionCount == other.TopologyVersionCount &&
            RebalanceEvaluationCount == other.RebalanceEvaluationCount &&
            AcceptedMoveCount == other.AcceptedMoveCount &&
            SkippedDecisionCount == other.SkippedDecisionCount &&
            DirectHotReliefCount == other.DirectHotReliefCount &&
            ColdEvacuationCount == other.ColdEvacuationCount &&
            FailedMigrationCount == other.FailedMigrationCount &&
            ValidationSucceeded == other.ValidationSucceeded &&
            ValidationChecksum == other.ValidationChecksum &&
            ProcessingValidationFailedBatchCount == other.ProcessingValidationFailedBatchCount &&
            HasSameSkippedReasonCounters(SkippedReasonCounters, other.SkippedReasonCounters) &&
            HasSameRetentionStats(RetentionStats, other.RetentionStats);

        private static bool HasSameSkippedReasonCounters(
            IReadOnlyList<RadarProcessingRebalanceSkippedReasonCounter>? current,
            IReadOnlyList<RadarProcessingRebalanceSkippedReasonCounter>? other)
        {
            var currentCount = current?.Count ?? 0;
            if (currentCount != (other?.Count ?? 0))
            {
                return false;
            }

            if (currentCount == 0)
            {
                return true;
            }

            var currentSorted = current!.OrderBy(counter => counter.Reason).ToArray();
            var otherSorted = other!.OrderBy(counter => counter.Reason).ToArray();
            for (var index = 0; index < currentSorted.Length; index++)
            {
                if (currentSorted[index].Reason != otherSorted[index].Reason ||
                    currentSorted[index].Count != otherSorted[index].Count)
                {
                    return false;
                }
            }

            return true;
        }

        private static RadarProcessingRebalanceRetentionStats AddRetentionStats(
            RadarProcessingRebalanceRetentionStats current,
            RadarProcessingRebalanceRetentionStats other) =>
            new(
                Math.Max(current.RetainedDecisionCount, other.RetainedDecisionCount),
                checked(current.DroppedDecisionCount + other.DroppedDecisionCount),
                Math.Max(
                    current.RetainedLifecycleTransitionCount,
                    other.RetainedLifecycleTransitionCount),
                checked(current.DroppedLifecycleTransitionCount + other.DroppedLifecycleTransitionCount),
                Math.Max(current.RetainedAcceptedMoveCount, other.RetainedAcceptedMoveCount),
                checked(current.DroppedAcceptedMoveCount + other.DroppedAcceptedMoveCount),
                Math.Max(current.RetainedValidationFailureCount, other.RetainedValidationFailureCount),
                checked(current.DroppedValidationFailureCount + other.DroppedValidationFailureCount));

        private static bool HasSameRetentionStats(
            RadarProcessingRebalanceRetentionStats current,
            RadarProcessingRebalanceRetentionStats other) =>
            current.RetainedDecisionCount == other.RetainedDecisionCount &&
            current.DroppedDecisionCount == other.DroppedDecisionCount &&
            current.RetainedLifecycleTransitionCount == other.RetainedLifecycleTransitionCount &&
            current.DroppedLifecycleTransitionCount == other.DroppedLifecycleTransitionCount &&
            current.RetainedAcceptedMoveCount == other.RetainedAcceptedMoveCount &&
            current.DroppedAcceptedMoveCount == other.DroppedAcceptedMoveCount &&
            current.RetainedValidationFailureCount == other.RetainedValidationFailureCount &&
            current.DroppedValidationFailureCount == other.DroppedValidationFailureCount;

        private static void AddDecision(
            RadarProcessingRebalanceDecision? decision,
            ref List<RadarProcessingRebalanceSkippedReason>? skippedReasons,
            ref List<RadarProcessingRebalanceSkippedReasonCounter>? skippedReasonCounters,
            ref long skippedDecisionCount)
        {
            if (decision is null || decision.HasAcceptedMove)
            {
                return;
            }

            skippedDecisionCount = checked(skippedDecisionCount + 1);
            foreach (var reason in decision.SkippedReasons)
            {
                AddSkippedReason(ref skippedReasons, reason);
                AddSkippedReasonCounter(ref skippedReasonCounters, reason, count: 1);
            }
        }

        private static void AddSkippedReason(
            ref List<RadarProcessingRebalanceSkippedReason>? skippedReasons,
            RadarProcessingRebalanceSkippedReason reason)
        {
            skippedReasons ??= new List<RadarProcessingRebalanceSkippedReason>();
            if (!skippedReasons.Contains(reason))
            {
                skippedReasons.Add(reason);
            }
        }

        private static void AddSkippedReasonCounter(
            ref List<RadarProcessingRebalanceSkippedReasonCounter>? skippedReasonCounters,
            RadarProcessingRebalanceSkippedReason reason,
            long count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            if (count == 0)
            {
                return;
            }

            skippedReasonCounters ??= new List<RadarProcessingRebalanceSkippedReasonCounter>();
            for (var index = 0; index < skippedReasonCounters.Count; index++)
            {
                if (skippedReasonCounters[index].Reason != reason)
                {
                    continue;
                }

                skippedReasonCounters[index] = new RadarProcessingRebalanceSkippedReasonCounter(
                    reason,
                    checked(skippedReasonCounters[index].Count + count));
                return;
            }

            skippedReasonCounters.Add(new RadarProcessingRebalanceSkippedReasonCounter(reason, count));
        }

        private static RadarProcessingSyntheticRebalanceMovePressure CreateMovePressure(
            RadarProcessingRebalanceDecision decision) =>
            new(
                decision.MoveKind,
                decision.ProjectedPressure.SourceShardBefore.Value,
                decision.ProjectedPressure.TargetShardBefore.Value,
                decision.ProjectedPressure.SourceShardAfter.Value,
                decision.ProjectedPressure.TargetShardAfter.Value,
                decision.ExpectedRelief);

        private static ulong ComputeChecksum(
            RadarProcessingMetrics metrics,
            long topologyVersionCount,
            long rebalanceEvaluationCount,
            long acceptedMoveCount,
            long skippedDecisionCount,
            long directHotReliefCount,
            long coldEvacuationCount,
            long failedMigrationCount,
            bool validationSucceeded)
        {
            var checksum = ChecksumInitial;
            checksum = AppendInt64(checksum, metrics.ProcessedBatchCount);
            checksum = AppendInt64(checksum, metrics.ProcessedStreamEventCount);
            checksum = AppendInt64(checksum, metrics.ProcessedPayloadValueCount);
            checksum = AppendInt64(checksum, metrics.ActiveSourceCount);
            checksum = AppendInt64(checksum, metrics.RawValueChecksum);
            checksum = AppendUInt64(checksum, metrics.ProcessingChecksum);
            checksum = AppendInt64(checksum, topologyVersionCount);
            checksum = AppendInt64(checksum, rebalanceEvaluationCount);
            checksum = AppendInt64(checksum, acceptedMoveCount);
            checksum = AppendInt64(checksum, skippedDecisionCount);
            checksum = AppendInt64(checksum, directHotReliefCount);
            checksum = AppendInt64(checksum, coldEvacuationCount);
            checksum = AppendInt64(checksum, failedMigrationCount);
            return AppendInt32(checksum, validationSucceeded ? 1 : 0);
        }
    }

    private struct CacheIterationTotals
    {
        public static CacheIterationTotals Empty => new();

        public long ExaminedFileCount;
        public long SkippedFileCount;
        public long PublishedFileCount;
        public long FileSizeBytes;
        public long CompressedRecordCount;
        public long CompressedBytes;
        public long DecompressedBytes;
        public long BatchCount;
        public long EventCount;
        public long PayloadBytes;
        public long PayloadValueCount;
        public long RawValueChecksum;

        public void Add(RadarPulse.Domain.Archive.ArchiveRadarEventBatchPublishResult result)
        {
            PublishedFileCount = checked(PublishedFileCount + 1);
            FileSizeBytes = checked(FileSizeBytes + result.FileSizeBytes);
            CompressedRecordCount = checked(CompressedRecordCount + result.CompressedRecordCount);
            CompressedBytes = checked(CompressedBytes + result.CompressedBytes);
            DecompressedBytes = checked(DecompressedBytes + result.DecompressedBytes);
            BatchCount = checked(BatchCount + result.BatchCount);
            EventCount = checked(EventCount + result.EventCount);
            PayloadBytes = checked(PayloadBytes + result.PayloadBytes);
            PayloadValueCount = checked(PayloadValueCount + result.PayloadValueCount);
            RawValueChecksum = checked(RawValueChecksum + result.RawValueChecksum);
        }
    }

    private readonly record struct QueuedArchivePublishResult(
        ArchiveRadarEventBatchPublishResult PublishResult,
        RadarProcessingProviderQueueTelemetrySummary QueueTelemetry,
        RadarProcessingRetainedPayloadTelemetrySummary RetentionTelemetry,
        RadarProcessingArchiveOverlapTelemetrySummary OverlapTelemetry);

    private readonly record struct QueuedArchiveCachePublishResult(
        CacheIterationTotals Totals,
        RadarProcessingProviderQueueTelemetrySummary QueueTelemetry,
        RadarProcessingRetainedPayloadTelemetrySummary RetentionTelemetry,
        RadarProcessingArchiveOverlapTelemetrySummary OverlapTelemetry);

    private sealed record DefaultRetainedPayloadPrewarm(
        RadarProcessingRetainedPayloadFactory Factory,
        RadarProcessingRetainedPayloadPrewarmResult Result);

    private sealed class CacheArchiveFileSelection
    {
        public CacheArchiveFileSelection(
            CacheIterationTotals totals,
            IReadOnlyList<FileInfo> baseDataFiles)
        {
            Totals = totals;
            BaseDataFiles = baseDataFiles ?? throw new ArgumentNullException(nameof(baseDataFiles));
        }

        public CacheIterationTotals Totals { get; }

        public IReadOnlyList<FileInfo> BaseDataFiles { get; }
    }
}
