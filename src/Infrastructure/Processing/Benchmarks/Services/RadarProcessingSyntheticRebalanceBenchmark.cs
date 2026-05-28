using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Measures deterministic synthetic rebalance scenarios across execution modes.
/// </summary>
public sealed class RadarProcessingSyntheticRebalanceBenchmark
{
    private const ulong ChecksumInitial = 14_695_981_039_346_656_037UL;
    private const ulong ChecksumPrime = 1_099_511_628_211UL;

    /// <summary>
    /// Synchronously measures a generated rebalance workload scenario.
    /// </summary>
    public RadarProcessingSyntheticRebalanceBenchmarkResult Measure(
        RadarProcessingSyntheticRebalanceWorkloadKind workloadKind,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        int iterations,
        int warmupIterations,
        CancellationToken cancellationToken = default,
        RadarProcessingRebalanceHardeningOptions? hardeningOptions = null,
        RadarProcessingExecutionMode executionMode = RadarProcessingExecutionMode.PartitionedBarrier,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null,
        int orderedActiveBatchCapacity = 1)
    {
        var workload = RadarProcessingSyntheticRebalanceWorkload.Create(workloadKind);
        return Measure(
            workload,
            mode,
            iterations,
            warmupIterations,
            cancellationToken,
            hardeningOptions,
            executionMode,
            asyncExecution,
            orderedActiveBatchCapacity);
    }

    /// <summary>
    /// Synchronously measures an already-created rebalance workload.
    /// </summary>
    public RadarProcessingSyntheticRebalanceBenchmarkResult Measure(
        RadarProcessingSyntheticRebalanceWorkload workload,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        int iterations,
        int warmupIterations,
        CancellationToken cancellationToken = default,
        RadarProcessingRebalanceHardeningOptions? hardeningOptions = null,
        RadarProcessingExecutionMode executionMode = RadarProcessingExecutionMode.PartitionedBarrier,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null,
        int orderedActiveBatchCapacity = 1) =>
        MeasureAsync(
                workload,
                mode,
                iterations,
                warmupIterations,
                cancellationToken,
                hardeningOptions,
                executionMode,
                asyncExecution,
                orderedActiveBatchCapacity)
            .AsTask()
            .GetAwaiter()
            .GetResult();

    /// <summary>
    /// Asynchronously measures an already-created rebalance workload.
    /// </summary>
    public async ValueTask<RadarProcessingSyntheticRebalanceBenchmarkResult> MeasureAsync(
        RadarProcessingSyntheticRebalanceWorkload workload,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        int iterations,
        int warmupIterations,
        CancellationToken cancellationToken = default,
        RadarProcessingRebalanceHardeningOptions? hardeningOptions = null,
        RadarProcessingExecutionMode executionMode = RadarProcessingExecutionMode.PartitionedBarrier,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null,
        int orderedActiveBatchCapacity = 1)
    {
        ArgumentNullException.ThrowIfNull(workload);
        EnsureKnownMode(mode);
        EnsureKnownExecutionMode(executionMode);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);
        ArgumentOutOfRangeException.ThrowIfNegative(warmupIterations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(orderedActiveBatchCapacity);
        var effectiveHardeningOptions = hardeningOptions ?? workload.HardeningOptions;
        var effectiveAsyncExecution = executionMode == RadarProcessingExecutionMode.AsyncShardTransport
            ? asyncExecution ?? new RadarProcessingAsyncExecutionOptions(workerCount: workload.ShardCount, queueCapacity: 1)
            : asyncExecution;

        var workerTelemetryRecorder = executionMode == RadarProcessingExecutionMode.AsyncShardTransport
            ? new RadarProcessingWorkerTelemetryRecorder(effectiveHardeningOptions.TelemetryRetention)
            : null;
        RadarProcessingAsyncWorkerGroup? workerGroup = null;
        try
        {
            workerGroup = executionMode == RadarProcessingExecutionMode.AsyncShardTransport
                ? new RadarProcessingAsyncWorkerGroup(
                    new RadarProcessingAsyncWorkerGroupOptions(effectiveAsyncExecution))
                : null;

            for (var warmupIteration = 0; warmupIteration < warmupIterations; warmupIteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await RunIterationAsync(
                    workload,
                    mode,
                    effectiveHardeningOptions,
                    executionMode,
                    effectiveAsyncExecution,
                    workerTelemetryRecorder: null,
                    workerGroup,
                    orderedActiveBatchCapacity,
                    cancellationToken).ConfigureAwait(false);
            }

            var allocationBefore = CaptureAllocationSnapshot(executionMode);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            IterationTelemetry? expectedIteration = null;
            var aggregate = IterationTelemetry.Empty;
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var iterationTelemetry = await RunIterationAsync(
                    workload,
                    mode,
                    effectiveHardeningOptions,
                    executionMode,
                    effectiveAsyncExecution,
                    workerTelemetryRecorder,
                    workerGroup,
                    orderedActiveBatchCapacity,
                    cancellationToken).ConfigureAwait(false);
                if (expectedIteration.HasValue && !expectedIteration.Value.HasSameStableTotals(iterationTelemetry))
                {
                    throw new InvalidDataException("Synthetic rebalance benchmark produced inconsistent iteration totals.");
                }

                expectedIteration ??= iterationTelemetry;
                aggregate = aggregate.Add(iterationTelemetry);
            }

            stopwatch.Stop();
            var allocatedBytes = CaptureAllocationSnapshot(executionMode).DeltaSince(allocationBefore);
            var allocationSummary = RadarProcessingRebalanceAllocationSummary.ForProcessingOnly(allocatedBytes);
            var measuredIteration = expectedIteration ??
                                    throw new InvalidOperationException("Synthetic rebalance benchmark did not run.");
            var workerTelemetry = workerTelemetryRecorder?.CreateSummary();
            if (workerTelemetry is not null)
            {
                var retentionValidation = RadarProcessingAsyncValidator.ValidateWorkerTelemetryRetention(
                    workerTelemetry,
                    workerTelemetryRecorder!.Options,
                    effectiveHardeningOptions.ValidationProfile);
                if (!retentionValidation.IsValid)
                {
                    throw new InvalidDataException(retentionValidation.Message);
                }
            }

            return new RadarProcessingSyntheticRebalanceBenchmarkResult(
                workload.Kind,
                mode,
                iterations,
                warmupIterations,
                workload.SourceCount,
                workload.PartitionCount,
                workload.ShardCount,
                workload.BatchesPerIteration,
                workload.EventsPerIteration,
                workload.PayloadValuesPerIteration,
                workload.RawValueChecksumPerIteration,
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
                CreateReadOnlyList(aggregate.AcceptedMovePressures),
                stopwatch.Elapsed,
                allocatedBytes,
                effectiveHardeningOptions.ValidationProfile,
                effectiveHardeningOptions.TelemetryRetention.RetentionMode,
                effectiveHardeningOptions.QuarantineLifecycle.QuarantineTtlEvaluations,
                effectiveHardeningOptions.QuarantineLifecycle.SustainedCoolingSampleCount,
                effectiveHardeningOptions.QuarantineLifecycle.MaterialPressureChangeThreshold,
                allocationSummary,
                executionMode,
                workerTelemetry,
                orderedActiveBatchCapacity);
        }
        finally
        {
            if (workerGroup is not null)
            {
                await workerGroup.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static ValueTask<IterationTelemetry> RunIterationAsync(
        RadarProcessingSyntheticRebalanceWorkload workload,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        RadarProcessingRebalanceHardeningOptions hardeningOptions,
        RadarProcessingExecutionMode executionMode,
        RadarProcessingAsyncExecutionOptions? asyncExecution,
        RadarProcessingWorkerTelemetryRecorder? workerTelemetryRecorder,
        RadarProcessingAsyncWorkerGroup? workerGroup,
        int orderedActiveBatchCapacity,
        CancellationToken cancellationToken) =>
        mode switch
        {
            RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance =>
                RunStaticIterationAsync(
                    workload,
                    executionMode,
                    asyncExecution,
                    workerTelemetryRecorder,
                    workerGroup,
                    cancellationToken),
            RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly =>
                RunPressureSamplingIterationAsync(
                    workload,
                    executionMode,
                    asyncExecution,
                    workerTelemetryRecorder,
                    workerGroup,
                    cancellationToken),
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession =>
                RunRebalanceSessionIterationAsync(
                    workload,
                    hardeningOptions,
                    executionMode,
                    asyncExecution,
                    workerTelemetryRecorder,
                    workerGroup,
                    cancellationToken),
            RadarProcessingSyntheticRebalanceBenchmarkMode.OrderedRebalanceSession =>
                RunOrderedRebalanceSessionIterationAsync(
                    workload,
                    hardeningOptions,
                    executionMode,
                    asyncExecution,
                    workerTelemetryRecorder,
                    workerGroup,
                    orderedActiveBatchCapacity,
                    cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

    private static async ValueTask<IterationTelemetry> RunStaticIterationAsync(
        RadarProcessingSyntheticRebalanceWorkload workload,
        RadarProcessingExecutionMode executionMode,
        RadarProcessingAsyncExecutionOptions? asyncExecution,
        RadarProcessingWorkerTelemetryRecorder? workerTelemetryRecorder,
        RadarProcessingAsyncWorkerGroup? workerGroup,
        CancellationToken cancellationToken)
    {
        var coreOptions = workload.CreateCoreOptions(executionMode, asyncExecution);
        var core = new RadarProcessingCore(workload.SourceUniverse, coreOptions);
        RadarProcessingAsyncCoreSession? asyncSession = null;
        try
        {
            asyncSession = executionMode == RadarProcessingExecutionMode.AsyncShardTransport
                ? CreateAsyncCoreSession(core, workerTelemetryRecorder, workerGroup)
                : null;
            foreach (var batch in workload.Batches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = asyncSession is null
                    ? core.Process(batch, cancellationToken)
                    : await asyncSession.ProcessAsync(batch, cancellationToken).ConfigureAwait(false);
                EnsureValidProcessingResult(result);
            }
        }
        finally
        {
            if (asyncSession is not null)
            {
                await asyncSession.DisposeAsync().ConfigureAwait(false);
            }
        }

        return IterationTelemetry.FromMetrics(
            core.CreateMetrics(),
            topologyVersionCount: 1);
    }

    private static async ValueTask<IterationTelemetry> RunPressureSamplingIterationAsync(
        RadarProcessingSyntheticRebalanceWorkload workload,
        RadarProcessingExecutionMode executionMode,
        RadarProcessingAsyncExecutionOptions? asyncExecution,
        RadarProcessingWorkerTelemetryRecorder? workerTelemetryRecorder,
        RadarProcessingAsyncWorkerGroup? workerGroup,
        CancellationToken cancellationToken)
    {
        var coreOptions = workload.CreateCoreOptions(executionMode, asyncExecution);
        var core = new RadarProcessingCore(workload.SourceUniverse, coreOptions);
        var pressureWindow = new RadarProcessingPressureWindow(workload.PressureWindowOptions);
        var evaluationCount = 0L;
        RadarProcessingAsyncCoreSession? asyncSession = null;

        try
        {
            asyncSession = executionMode == RadarProcessingExecutionMode.AsyncShardTransport
                ? CreateAsyncCoreSession(core, workerTelemetryRecorder, workerGroup)
                : null;
            foreach (var batch in workload.Batches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = asyncSession is null
                    ? core.Process(batch, cancellationToken)
                    : await asyncSession.ProcessAsync(batch, cancellationToken).ConfigureAwait(false);
                EnsureValidProcessingResult(result);
                var telemetry = result.Telemetry ??
                                throw new InvalidDataException("Pressure sampling benchmark requires telemetry.");
                pressureWindow.AddSample(RadarProcessingPressureSample.FromTelemetry(telemetry, workload.PressureOptions));
                evaluationCount = checked(evaluationCount + 1);
            }
        }
        finally
        {
            if (asyncSession is not null)
            {
                await asyncSession.DisposeAsync().ConfigureAwait(false);
            }
        }

        return IterationTelemetry.FromMetrics(
            core.CreateMetrics(),
            topologyVersionCount: 1,
            rebalanceEvaluationCount: evaluationCount);
    }

    private static async ValueTask<IterationTelemetry> RunRebalanceSessionIterationAsync(
        RadarProcessingSyntheticRebalanceWorkload workload,
        RadarProcessingRebalanceHardeningOptions hardeningOptions,
        RadarProcessingExecutionMode executionMode,
        RadarProcessingAsyncExecutionOptions? asyncExecution,
        RadarProcessingWorkerTelemetryRecorder? workerTelemetryRecorder,
        RadarProcessingAsyncWorkerGroup? workerGroup,
        CancellationToken cancellationToken)
    {
        var session = workload.CreateSession(hardeningOptions, executionMode, asyncExecution);
        var initialTopologyVersion = session.CurrentTopology.Version;
        var telemetry = IterationTelemetry.Empty;
        RadarProcessingAsyncRebalanceSession? asyncSession = null;

        try
        {
            asyncSession = executionMode == RadarProcessingExecutionMode.AsyncShardTransport
                ? new RadarProcessingAsyncRebalanceSession(
                    session,
                    CreateAsyncCoreSession(session.Core, workerTelemetryRecorder, workerGroup),
                    ownsAsyncCoreSession: true)
                : null;
            foreach (var batch in workload.Batches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = asyncSession is null
                    ? session.Process(batch, cancellationToken)
                    : await asyncSession.ProcessAsync(batch, cancellationToken).ConfigureAwait(false);
                telemetry = telemetry.Add(result);
            }
        }
        finally
        {
            if (asyncSession is not null)
            {
                await asyncSession.DisposeAsync().ConfigureAwait(false);
            }
        }

        var metrics = session.Core.CreateMetrics();
        return telemetry.WithMetrics(
            metrics,
            session.CurrentTopology.Version.Value - initialTopologyVersion.Value + 1);
    }

    private static async ValueTask<IterationTelemetry> RunOrderedRebalanceSessionIterationAsync(
        RadarProcessingSyntheticRebalanceWorkload workload,
        RadarProcessingRebalanceHardeningOptions hardeningOptions,
        RadarProcessingExecutionMode executionMode,
        RadarProcessingAsyncExecutionOptions? asyncExecution,
        RadarProcessingWorkerTelemetryRecorder? workerTelemetryRecorder,
        RadarProcessingAsyncWorkerGroup? workerGroup,
        int orderedActiveBatchCapacity,
        CancellationToken cancellationToken)
    {
        var session = workload.CreateSession(hardeningOptions, executionMode, asyncExecution);
        var initialTopologyVersion = session.CurrentTopology.Version;
        var telemetry = IterationTelemetry.Empty;
        var queueCapacity = Math.Max(orderedActiveBatchCapacity, checked((int)workload.BatchesPerIteration));
        var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(queueCapacity));
        RadarProcessingAsyncRebalanceSession? asyncSession = null;
        if (executionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            asyncSession = new RadarProcessingAsyncRebalanceSession(
                session,
                CreateAsyncCoreSession(session.Core, workerTelemetryRecorder, workerGroup),
                ownsAsyncCoreSession: true);
        }

        await using var queuedSession = new RadarProcessingQueuedRebalanceSession(
            session,
            queue,
            asyncSession,
            ownsQueue: true,
            ownsAsyncRebalanceSession: asyncSession is not null);

        foreach (var batch in workload.Batches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var enqueue = await queuedSession
                .EnqueueAsync(batch, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (!enqueue.IsAccepted)
            {
                throw new InvalidDataException($"Ordered rebalance synthetic enqueue failed with status {enqueue.Status}.");
            }
        }

        queuedSession.CompleteAdding();
        var result = await queuedSession
            .DrainOrderedConcurrentAsync(
                new RadarProcessingOrderedConcurrencyOptions(orderedActiveBatchCapacity),
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsCompleted)
        {
            throw new InvalidDataException(result.Message);
        }

        foreach (var processing in result.ProcessingResults)
        {
            if (!processing.IsSuccessful || processing.RebalanceResult is null)
            {
                throw new InvalidDataException(processing.Message);
            }

            telemetry = telemetry.Add(processing.RebalanceResult);
        }

        var metrics = session.Core.CreateMetrics();
        return telemetry.WithMetrics(
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
            throw new InvalidDataException("Synthetic rebalance benchmark requires partitioned telemetry.");
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

    private static void EnsureKnownMode(RadarProcessingSyntheticRebalanceBenchmarkMode mode)
    {
        if (mode is not RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance and
            not RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly and
            not RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession and
            not RadarProcessingSyntheticRebalanceBenchmarkMode.OrderedRebalanceSession)
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

    private static RadarProcessingBenchmarkAllocationSnapshot CaptureAllocationSnapshot(
        RadarProcessingExecutionMode executionMode) =>
        executionMode == RadarProcessingExecutionMode.AsyncShardTransport
            ? RadarProcessingBenchmarkAllocationSnapshot.Capture()
            : RadarProcessingBenchmarkAllocationSnapshot.CaptureCurrentThread();

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

    private readonly record struct IterationTelemetry(
        long ProcessedBatchCount,
        long ProcessedEventCount,
        long ProcessedPayloadValueCount,
        long RawValueChecksum,
        long ActiveSourceCount,
        ulong ProcessingChecksum,
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
        List<RadarProcessingSyntheticRebalanceMovePressure>? AcceptedMovePressures)
    {
        public static IterationTelemetry Empty =>
            new(
                ProcessedBatchCount: 0,
                ProcessedEventCount: 0,
                ProcessedPayloadValueCount: 0,
                RawValueChecksum: 0,
                ActiveSourceCount: 0,
                ProcessingChecksum: 0,
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
                AcceptedMovePressures: null);

        public static IterationTelemetry FromMetrics(
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

        public IterationTelemetry Add(RadarProcessingRebalanceSessionResult result)
        {
            var skippedReasons = SkippedReasons;
            var movePressures = AcceptedMovePressures;
            var skippedDecisionCount = SkippedDecisionCount;
            var acceptedMoveCount = AcceptedMoveCount;
            var directHotReliefCount = DirectHotReliefCount;
            var coldEvacuationCount = ColdEvacuationCount;
            var failedMigrationCount = FailedMigrationCount;

            AddDecision(result.DirectHotReliefDecision, ref skippedReasons, ref skippedDecisionCount);
            AddDecision(result.ColdEvacuationDecision, ref skippedReasons, ref skippedDecisionCount);

            if (result.PublishedMigration)
            {
                acceptedMoveCount = checked(acceptedMoveCount + 1);
                var decision = result.RebalanceDecision ??
                               throw new InvalidDataException("Published moves require a rebalance decision.");
                movePressures ??= new List<RadarProcessingSyntheticRebalanceMovePressure>();
                movePressures.Add(CreateMovePressure(decision));
                if (decision.MoveKind == RadarProcessingRebalanceMoveKind.DirectHotRelief)
                {
                    directHotReliefCount = checked(directHotReliefCount + 1);
                }
                else if (decision.MoveKind == RadarProcessingRebalanceMoveKind.ColdEvacuation)
                {
                    coldEvacuationCount = checked(coldEvacuationCount + 1);
                }
            }

            if (result.MigrationResult is not null && !result.MigrationResult.Succeeded)
            {
                failedMigrationCount = checked(failedMigrationCount + 1);
            }

            return this with
            {
                RebalanceEvaluationCount = checked(RebalanceEvaluationCount + 1),
                AcceptedMoveCount = acceptedMoveCount,
                SkippedDecisionCount = skippedDecisionCount,
                DirectHotReliefCount = directHotReliefCount,
                ColdEvacuationCount = coldEvacuationCount,
                FailedMigrationCount = failedMigrationCount,
                ValidationSucceeded = ValidationSucceeded && result.Validation.IsValid,
                SkippedReasons = skippedReasons,
                AcceptedMovePressures = movePressures
            };
        }

        public IterationTelemetry Add(IterationTelemetry other)
        {
            var skippedReasons = SkippedReasons;
            if (other.SkippedReasons is { Count: > 0 } otherSkippedReasons)
            {
                foreach (var reason in otherSkippedReasons)
                {
                    AddSkippedReason(ref skippedReasons, reason);
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
                ProcessedBatchCount = checked(ProcessedBatchCount + other.ProcessedBatchCount),
                ProcessedEventCount = checked(ProcessedEventCount + other.ProcessedEventCount),
                ProcessedPayloadValueCount = checked(
                    ProcessedPayloadValueCount + other.ProcessedPayloadValueCount),
                RawValueChecksum = checked(RawValueChecksum + other.RawValueChecksum),
                ActiveSourceCount = other.ActiveSourceCount,
                ProcessingChecksum = other.ProcessingChecksum,
                TopologyVersionCount = other.TopologyVersionCount,
                RebalanceEvaluationCount = checked(RebalanceEvaluationCount + other.RebalanceEvaluationCount),
                AcceptedMoveCount = checked(AcceptedMoveCount + other.AcceptedMoveCount),
                SkippedDecisionCount = checked(SkippedDecisionCount + other.SkippedDecisionCount),
                DirectHotReliefCount = checked(DirectHotReliefCount + other.DirectHotReliefCount),
                ColdEvacuationCount = checked(ColdEvacuationCount + other.ColdEvacuationCount),
                FailedMigrationCount = checked(FailedMigrationCount + other.FailedMigrationCount),
                ValidationSucceeded = ValidationSucceeded && other.ValidationSucceeded,
                ValidationChecksum = AppendUInt64(ValidationChecksum, other.ValidationChecksum),
                SkippedReasons = skippedReasons,
                AcceptedMovePressures = movePressures
            };
        }

        public IterationTelemetry WithMetrics(
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
                ProcessedBatchCount = metrics.ProcessedBatchCount,
                ProcessedEventCount = metrics.ProcessedStreamEventCount,
                ProcessedPayloadValueCount = metrics.ProcessedPayloadValueCount,
                RawValueChecksum = metrics.RawValueChecksum,
                ActiveSourceCount = metrics.ActiveSourceCount,
                ProcessingChecksum = metrics.ProcessingChecksum,
                TopologyVersionCount = topologyVersionCount,
                ValidationChecksum = validationChecksum
            };
        }

        public bool HasSameStableTotals(IterationTelemetry other) =>
            ProcessedBatchCount == other.ProcessedBatchCount &&
            ProcessedEventCount == other.ProcessedEventCount &&
            ProcessedPayloadValueCount == other.ProcessedPayloadValueCount &&
            RawValueChecksum == other.RawValueChecksum &&
            ActiveSourceCount == other.ActiveSourceCount &&
            TopologyVersionCount == other.TopologyVersionCount &&
            RebalanceEvaluationCount == other.RebalanceEvaluationCount &&
            AcceptedMoveCount == other.AcceptedMoveCount &&
            SkippedDecisionCount == other.SkippedDecisionCount &&
            DirectHotReliefCount == other.DirectHotReliefCount &&
            ColdEvacuationCount == other.ColdEvacuationCount &&
            FailedMigrationCount == other.FailedMigrationCount &&
            ValidationSucceeded == other.ValidationSucceeded &&
            ValidationChecksum == other.ValidationChecksum;

        private static void AddDecision(
            RadarProcessingRebalanceDecision? decision,
            ref List<RadarProcessingRebalanceSkippedReason>? skippedReasons,
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
}
