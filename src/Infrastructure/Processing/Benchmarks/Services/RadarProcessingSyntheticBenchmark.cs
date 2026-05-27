using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingSyntheticBenchmark
{
    private const ulong ChecksumInitial = 14_695_981_039_346_656_037UL;
    private const ulong ChecksumPrime = 1_099_511_628_211UL;

    public RadarProcessingBenchmarkResult Measure(
        RadarProcessingSyntheticWorkloadOptions workloadOptions,
        RadarProcessingExecutionMode executionMode,
        int partitionCount,
        int shardCount,
        RadarProcessingBenchmarkHandlerSet handlerSet,
        int iterations,
        int warmupIterations,
        CancellationToken cancellationToken,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null,
        RadarProcessingValidationProfile validationProfile = RadarProcessingValidationProfile.Benchmark) =>
        MeasureAsync(
                workloadOptions,
                executionMode,
                partitionCount,
                shardCount,
                handlerSet,
                iterations,
                warmupIterations,
                cancellationToken,
                asyncExecution,
                validationProfile)
            .AsTask()
            .GetAwaiter()
            .GetResult();

    public async ValueTask<RadarProcessingBenchmarkResult> MeasureAsync(
        RadarProcessingSyntheticWorkloadOptions workloadOptions,
        RadarProcessingExecutionMode executionMode,
        int partitionCount,
        int shardCount,
        RadarProcessingBenchmarkHandlerSet handlerSet,
        int iterations,
        int warmupIterations,
        CancellationToken cancellationToken,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null,
        RadarProcessingValidationProfile validationProfile = RadarProcessingValidationProfile.Benchmark)
    {
        var workload = RadarProcessingSyntheticWorkload.Create(workloadOptions);
        return await MeasureAsync(
            workload,
            executionMode,
            partitionCount,
            shardCount,
            handlerSet,
            iterations,
            warmupIterations,
            cancellationToken,
            asyncExecution,
            validationProfile).ConfigureAwait(false);
    }

    public RadarProcessingBenchmarkResult Measure(
        RadarProcessingSyntheticWorkload workload,
        RadarProcessingExecutionMode executionMode,
        int partitionCount,
        int shardCount,
        RadarProcessingBenchmarkHandlerSet handlerSet,
        int iterations,
        int warmupIterations,
        CancellationToken cancellationToken,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null,
        RadarProcessingValidationProfile validationProfile = RadarProcessingValidationProfile.Benchmark) =>
        MeasureAsync(
                workload,
                executionMode,
                partitionCount,
                shardCount,
                handlerSet,
                iterations,
                warmupIterations,
                cancellationToken,
                asyncExecution,
                validationProfile)
            .AsTask()
            .GetAwaiter()
            .GetResult();

    public async ValueTask<RadarProcessingBenchmarkResult> MeasureAsync(
        RadarProcessingSyntheticWorkload workload,
        RadarProcessingExecutionMode executionMode,
        int partitionCount,
        int shardCount,
        RadarProcessingBenchmarkHandlerSet handlerSet,
        int iterations,
        int warmupIterations,
        CancellationToken cancellationToken,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null,
        RadarProcessingValidationProfile validationProfile = RadarProcessingValidationProfile.Benchmark)
    {
        ArgumentNullException.ThrowIfNull(workload);
        EnsureKnownExecutionMode(executionMode);
        EnsureKnownHandlerSet(handlerSet);
        RadarProcessingRebalanceHardeningOptions.EnsureKnownValidationProfile(validationProfile);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);
        ArgumentOutOfRangeException.ThrowIfNegative(warmupIterations);

        var effectiveAsyncExecution = executionMode == RadarProcessingExecutionMode.AsyncShardTransport
            ? asyncExecution ?? new RadarProcessingAsyncExecutionOptions(workerCount: shardCount, queueCapacity: 1)
            : asyncExecution;
        var coreOptions = CreateCoreOptions(
            executionMode,
            partitionCount,
            shardCount,
            handlerSet,
            effectiveAsyncExecution);
        var shardDistributions = CreateShardDistributions(workload, coreOptions);
        var validation = await ComputeValidationChecksumAsync(
            workload,
            coreOptions,
            handlerSet,
            validationProfile,
            cancellationToken).ConfigureAwait(false);
        var core = new RadarProcessingCore(workload.SourceUniverse, coreOptions);
        RadarProcessingAsyncCoreSession? asyncSession = null;

        try
        {
            asyncSession = coreOptions.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport
                ? new RadarProcessingAsyncCoreSession(core)
                : null;

            for (var warmupIteration = 0; warmupIteration < warmupIterations; warmupIteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ProcessAndValidateIterationAsync(
                    core,
                    asyncSession,
                    workload,
                    coreOptions,
                    expectedIteration: null,
                    cancellationToken).ConfigureAwait(false);
            }

            asyncSession?.WorkerTelemetryRecorder.Reset();

            var allocatedBytesBefore = GC.GetTotalAllocatedBytes(precise: true);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            IterationTotals? expectedIteration = null;
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var iterationResult = await ProcessAndValidateIterationAsync(
                    core,
                    asyncSession,
                    workload,
                    coreOptions,
                    expectedIteration,
                    cancellationToken).ConfigureAwait(false);
                expectedIteration ??= iterationResult.Totals;
            }

            stopwatch.Stop();
            var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBytesBefore;
            var measuredIteration = expectedIteration ??
                                    throw new InvalidOperationException("Processing benchmark did not run any iterations.");
            var workerTelemetry = asyncSession?.WorkerTelemetryRecorder.CreateSummary();
            if (workerTelemetry is not null)
            {
                var retentionValidation = RadarProcessingAsyncValidator.ValidateWorkerTelemetryRetention(
                    workerTelemetry,
                    asyncSession!.WorkerTelemetryRecorder.Options,
                    validationProfile);
                if (!retentionValidation.IsValid)
                {
                    throw new InvalidDataException(retentionValidation.Message);
                }
            }

            return new RadarProcessingBenchmarkResult(
                executionMode,
                partitionCount,
                shardCount,
                handlerSet,
                iterations,
                warmupIterations,
                workload.SourceUniverse.SourceCount,
                measuredIteration.BatchCount,
                measuredIteration.EventCount,
                measuredIteration.PayloadValueCount,
                measuredIteration.RawValueChecksum,
                measuredIteration.ActiveSourceCount,
                validation.Checksum,
                shardDistributions,
                stopwatch.Elapsed,
                allocatedBytes,
                validationProfile,
                workerTelemetry,
                validation.AsyncValidation);
        }
        finally
        {
            if (asyncSession is not null)
            {
                await asyncSession.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static RadarProcessingCoreOptions CreateCoreOptions(
        RadarProcessingExecutionMode executionMode,
        int partitionCount,
        int shardCount,
        RadarProcessingBenchmarkHandlerSet handlerSet,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null) =>
        new(
            executionMode,
            partitionCount,
            shardCount,
            enableValidation: true,
            CreateHandlers(handlerSet),
            asyncExecution);

    private static IReadOnlyList<IRadarSourceProcessingHandler> CreateHandlers(
        RadarProcessingBenchmarkHandlerSet handlerSet) =>
        RadarProcessingBenchmarkHandlers.Create(handlerSet);

    private static async ValueTask<ProcessingIterationResult> ProcessAndValidateIterationAsync(
        RadarProcessingCore core,
        RadarProcessingAsyncCoreSession? asyncSession,
        RadarProcessingSyntheticWorkload workload,
        RadarProcessingCoreOptions coreOptions,
        IterationTotals? expectedIteration,
        CancellationToken cancellationToken)
    {
        var before = core.CreateMetrics();
        RadarProcessingResult? lastResult = null;

        foreach (var batch in workload.Batches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = asyncSession is null
                ? core.Process(batch, cancellationToken)
                : await asyncSession.ProcessAsync(batch, cancellationToken).ConfigureAwait(false);
            if (!result.IsValid)
            {
                throw new InvalidDataException(result.Validation.Message);
            }

            if ((coreOptions.ExecutionMode == RadarProcessingExecutionMode.PartitionedBarrier ||
                 coreOptions.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport) &&
                result.Telemetry is null)
            {
                throw new InvalidDataException("Partitioned or async processing benchmark did not produce telemetry.");
            }

            if (coreOptions.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport)
            {
                var asyncValidation = RadarProcessingAsyncValidator.ValidateProcessingResult(
                    result,
                    RadarProcessingValidationProfile.Benchmark);
                if (!asyncValidation.IsValid)
                {
                    throw new InvalidDataException(asyncValidation.Message);
                }
            }

            lastResult = result;
        }

        if (lastResult is null)
        {
            throw new InvalidOperationException("Processing benchmark workload has no batches.");
        }

        var after = core.CreateMetrics();
        var iterationTotals = IterationTotals.Create(before, after);
        if (iterationTotals.BatchCount != workload.BatchesPerIteration ||
            iterationTotals.EventCount != workload.EventsPerIteration ||
            iterationTotals.PayloadValueCount != workload.PayloadValuesPerIteration ||
            iterationTotals.RawValueChecksum != workload.RawValueChecksumPerIteration)
        {
            throw new InvalidDataException("Processing benchmark iteration totals do not match the workload contract.");
        }

        if (expectedIteration.HasValue && !expectedIteration.Value.HasSameTotals(iterationTotals))
        {
            throw new InvalidDataException("Processing benchmark produced inconsistent iteration totals.");
        }

        return new ProcessingIterationResult(iterationTotals, lastResult);
    }

    private static async ValueTask<ValidationRunResult> ComputeValidationChecksumAsync(
        RadarProcessingSyntheticWorkload workload,
        RadarProcessingCoreOptions coreOptions,
        RadarProcessingBenchmarkHandlerSet handlerSet,
        RadarProcessingValidationProfile validationProfile,
        CancellationToken cancellationToken)
    {
        if (coreOptions.ExecutionMode != RadarProcessingExecutionMode.AsyncShardTransport)
        {
            var core = new RadarProcessingCore(workload.SourceUniverse, coreOptions);
            foreach (var batch in workload.Batches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = core.Process(batch, cancellationToken);
                if (!result.IsValid)
                {
                    throw new InvalidDataException(result.Validation.Message);
                }
            }

            return new ValidationRunResult(ComputeStateChecksum(core), AsyncValidation: null);
        }

        var referenceOptions = CreateCoreOptions(
            RadarProcessingExecutionMode.PartitionedBarrier,
            coreOptions.PartitionCount,
            coreOptions.ShardCount,
            handlerSet);
        var referenceCore = new RadarProcessingCore(workload.SourceUniverse, referenceOptions);
        var asyncCore = new RadarProcessingCore(workload.SourceUniverse, coreOptions);
        RadarProcessingResult? referenceResult = null;
        RadarProcessingResult? asyncResult = null;

        await using (var asyncSession = new RadarProcessingAsyncCoreSession(asyncCore))
        {
            foreach (var batch in workload.Batches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                referenceResult = referenceCore.Process(batch, cancellationToken);
                asyncResult = await asyncSession.ProcessAsync(batch, cancellationToken).ConfigureAwait(false);
                if (!referenceResult.IsValid)
                {
                    throw new InvalidDataException(referenceResult.Validation.Message);
                }

                if (!asyncResult.IsValid)
                {
                    throw new InvalidDataException(asyncResult.Validation.Message);
                }
            }
        }

        if (referenceResult is null || asyncResult is null)
        {
            throw new InvalidOperationException("Processing benchmark workload has no batches.");
        }

        var asyncValidation = RadarProcessingAsyncValidator.ValidateBenchmarkComparison(
            referenceResult,
            asyncResult,
            referenceCore.CreateSourceSnapshots(),
            asyncCore.CreateSourceSnapshots(),
            validationProfile);
        if (!asyncValidation.IsValid)
        {
            throw new InvalidDataException(asyncValidation.Message);
        }

        if (!asyncValidation.HasComparisonChecksums)
        {
            asyncValidation = RadarProcessingAsyncValidationResult.Valid(
                RadarProcessingValidationProfile.Benchmark,
                referenceCore.CreateMetrics().ProcessingChecksum,
                asyncCore.CreateMetrics().ProcessingChecksum);
        }

        return new ValidationRunResult(ComputeStateChecksum(asyncCore), asyncValidation);
    }

    private static ulong ComputeStateChecksum(
        RadarProcessingCore core)
    {
        var metrics = core.CreateMetrics();
        var checksum = ChecksumInitial;
        checksum = AppendInt64(checksum, metrics.ProcessedBatchCount);
        checksum = AppendInt64(checksum, metrics.ProcessedStreamEventCount);
        checksum = AppendInt64(checksum, metrics.ProcessedPayloadValueCount);
        checksum = AppendInt64(checksum, metrics.ActiveSourceCount);
        checksum = AppendInt64(checksum, metrics.RawValueChecksum);
        checksum = AppendUInt64(checksum, metrics.ProcessingChecksum);

        foreach (var snapshot in core.CreateSourceHandlerSnapshots())
        {
            checksum = AppendInt32(checksum, snapshot.SourceId);
            foreach (var value in snapshot.Values)
            {
                checksum = AppendStringOrdinal(checksum, value.Name);
                checksum = AppendInt32(checksum, (int)value.Type);
                checksum = value.Type switch
                {
                    RadarSourceProcessingSnapshotFieldType.Int64 =>
                        AppendInt64(checksum, value.Int64Value),
                    RadarSourceProcessingSnapshotFieldType.Double =>
                        AppendUInt64(checksum, (ulong)BitConverter.DoubleToInt64Bits(value.DoubleValue)),
                    _ => throw new InvalidOperationException("Unsupported handler snapshot value type.")
                };
            }
        }

        return checksum;
    }

    private static IReadOnlyList<RadarProcessingBenchmarkShardDistribution> CreateShardDistributions(
        RadarProcessingSyntheticWorkload workload,
        RadarProcessingCoreOptions coreOptions)
    {
        var topology = new RadarProcessingTopology(workload.SourceUniverse, coreOptions);
        var shardEventCounts = new long[topology.ShardCount];

        foreach (var batch in workload.Batches)
        {
            foreach (var streamEvent in batch.Events.Span)
            {
                shardEventCounts[topology.GetShardIdForSource(streamEvent.SourceId)]++;
            }
        }

        var result = new RadarProcessingBenchmarkShardDistribution[shardEventCounts.Length];
        for (var shardId = 0; shardId < result.Length; shardId++)
        {
            result[shardId] = new RadarProcessingBenchmarkShardDistribution(
                shardId,
                shardEventCounts[shardId]);
        }

        return Array.AsReadOnly(result);
    }

    private static void EnsureKnownHandlerSet(RadarProcessingBenchmarkHandlerSet handlerSet)
    {
        RadarProcessingBenchmarkHandlers.EnsureKnown(handlerSet);
    }

    private static void EnsureKnownExecutionMode(RadarProcessingExecutionMode executionMode)
    {
        if (executionMode is not RadarProcessingExecutionMode.Sequential and
            not RadarProcessingExecutionMode.PartitionedBarrier and
            not RadarProcessingExecutionMode.AsyncShardTransport)
        {
            throw new ArgumentOutOfRangeException(nameof(executionMode));
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

    private static ulong AppendStringOrdinal(ulong checksum, string value)
    {
        checksum = AppendInt32(checksum, value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            checksum = AppendUInt32(checksum, value[i]);
        }

        return checksum;
    }

    private readonly record struct ValidationRunResult(
        ulong Checksum,
        RadarProcessingAsyncValidationResult? AsyncValidation);

    private readonly record struct ProcessingIterationResult(
        IterationTotals Totals,
        RadarProcessingResult LastResult);

    private readonly record struct IterationTotals(
        long BatchCount,
        long EventCount,
        long PayloadValueCount,
        long RawValueChecksum,
        long ActiveSourceCount)
    {
        public static IterationTotals Create(
            RadarProcessingMetrics before,
            RadarProcessingMetrics after) =>
            new(
                checked(after.ProcessedBatchCount - before.ProcessedBatchCount),
                checked(after.ProcessedStreamEventCount - before.ProcessedStreamEventCount),
                checked(after.ProcessedPayloadValueCount - before.ProcessedPayloadValueCount),
                checked(after.RawValueChecksum - before.RawValueChecksum),
                after.ActiveSourceCount);

        public bool HasSameTotals(IterationTotals other) =>
            BatchCount == other.BatchCount &&
            EventCount == other.EventCount &&
            PayloadValueCount == other.PayloadValueCount &&
            RawValueChecksum == other.RawValueChecksum &&
            ActiveSourceCount == other.ActiveSourceCount;
    }

}
