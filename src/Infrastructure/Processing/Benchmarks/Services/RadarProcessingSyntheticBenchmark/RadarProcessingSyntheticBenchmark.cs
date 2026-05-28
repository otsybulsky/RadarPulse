using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Measures deterministic synthetic processing throughput and allocation.
/// </summary>
public sealed partial class RadarProcessingSyntheticBenchmark
{
    private const ulong ChecksumInitial = 14_695_981_039_346_656_037UL;
    private const ulong ChecksumPrime = 1_099_511_628_211UL;

    /// <summary>
    /// Synchronously measures a generated synthetic workload.
    /// </summary>
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

    /// <summary>
    /// Asynchronously measures a generated synthetic workload.
    /// </summary>
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

    /// <summary>
    /// Synchronously measures an already-created synthetic workload.
    /// </summary>
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

    /// <summary>
    /// Asynchronously measures an already-created synthetic workload.
    /// </summary>
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
}
