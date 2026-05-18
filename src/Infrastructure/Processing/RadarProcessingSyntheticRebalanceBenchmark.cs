using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingSyntheticRebalanceBenchmark
{
    private const ulong ChecksumInitial = 14_695_981_039_346_656_037UL;
    private const ulong ChecksumPrime = 1_099_511_628_211UL;

    public RadarProcessingSyntheticRebalanceBenchmarkResult Measure(
        RadarProcessingSyntheticRebalanceWorkloadKind workloadKind,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        int iterations,
        int warmupIterations,
        CancellationToken cancellationToken = default,
        RadarProcessingRebalanceHardeningOptions? hardeningOptions = null)
    {
        var workload = RadarProcessingSyntheticRebalanceWorkload.Create(workloadKind);
        return Measure(workload, mode, iterations, warmupIterations, cancellationToken, hardeningOptions);
    }

    public RadarProcessingSyntheticRebalanceBenchmarkResult Measure(
        RadarProcessingSyntheticRebalanceWorkload workload,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        int iterations,
        int warmupIterations,
        CancellationToken cancellationToken = default,
        RadarProcessingRebalanceHardeningOptions? hardeningOptions = null)
    {
        ArgumentNullException.ThrowIfNull(workload);
        EnsureKnownMode(mode);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);
        ArgumentOutOfRangeException.ThrowIfNegative(warmupIterations);
        var effectiveHardeningOptions = hardeningOptions ?? workload.HardeningOptions;

        for (var warmupIteration = 0; warmupIteration < warmupIterations; warmupIteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RunIteration(workload, mode, effectiveHardeningOptions, cancellationToken);
        }

        var allocationBefore = RadarProcessingBenchmarkAllocationSnapshot.Capture();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        IterationTelemetry? expectedIteration = null;
        var aggregate = IterationTelemetry.Empty;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var iterationTelemetry = RunIteration(workload, mode, effectiveHardeningOptions, cancellationToken);
            if (expectedIteration.HasValue && !expectedIteration.Value.HasSameStableTotals(iterationTelemetry))
            {
                throw new InvalidDataException("Synthetic rebalance benchmark produced inconsistent iteration totals.");
            }

            expectedIteration ??= iterationTelemetry;
            aggregate = aggregate.Add(iterationTelemetry);
        }

        stopwatch.Stop();
        var allocatedBytes = RadarProcessingBenchmarkAllocationSnapshot.Capture().DeltaSince(allocationBefore);
        var allocationSummary = RadarProcessingRebalanceAllocationSummary.ForProcessingOnly(allocatedBytes);
        var measuredIteration = expectedIteration ??
                                throw new InvalidOperationException("Synthetic rebalance benchmark did not run.");

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
            allocationSummary);
    }

    private static IterationTelemetry RunIteration(
        RadarProcessingSyntheticRebalanceWorkload workload,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        RadarProcessingRebalanceHardeningOptions hardeningOptions,
        CancellationToken cancellationToken) =>
        mode switch
        {
            RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance =>
                RunStaticIteration(workload, cancellationToken),
            RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly =>
                RunPressureSamplingIteration(workload, cancellationToken),
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession =>
                RunRebalanceSessionIteration(workload, hardeningOptions, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

    private static IterationTelemetry RunStaticIteration(
        RadarProcessingSyntheticRebalanceWorkload workload,
        CancellationToken cancellationToken)
    {
        var core = new RadarProcessingCore(workload.SourceUniverse, workload.CoreOptions);
        foreach (var batch in workload.Batches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = core.Process(batch, cancellationToken);
            EnsureValidProcessingResult(result);
        }

        return IterationTelemetry.FromMetrics(
            core.CreateMetrics(),
            topologyVersionCount: 1);
    }

    private static IterationTelemetry RunPressureSamplingIteration(
        RadarProcessingSyntheticRebalanceWorkload workload,
        CancellationToken cancellationToken)
    {
        var core = new RadarProcessingCore(workload.SourceUniverse, workload.CoreOptions);
        var pressureWindow = new RadarProcessingPressureWindow(workload.PressureWindowOptions);
        var evaluationCount = 0L;

        foreach (var batch in workload.Batches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = core.Process(batch, cancellationToken);
            EnsureValidProcessingResult(result);
            var telemetry = result.Telemetry ??
                            throw new InvalidDataException("Pressure sampling benchmark requires telemetry.");
            pressureWindow.AddSample(RadarProcessingPressureSample.FromTelemetry(telemetry, workload.PressureOptions));
            evaluationCount = checked(evaluationCount + 1);
        }

        return IterationTelemetry.FromMetrics(
            core.CreateMetrics(),
            topologyVersionCount: 1,
            rebalanceEvaluationCount: evaluationCount);
    }

    private static IterationTelemetry RunRebalanceSessionIteration(
        RadarProcessingSyntheticRebalanceWorkload workload,
        RadarProcessingRebalanceHardeningOptions hardeningOptions,
        CancellationToken cancellationToken)
    {
        var session = workload.CreateSession(hardeningOptions);
        var initialTopologyVersion = session.CurrentTopology.Version;
        var telemetry = IterationTelemetry.Empty;

        foreach (var batch in workload.Batches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = session.Process(batch, cancellationToken);
            telemetry = telemetry.Add(result);
        }

        var metrics = session.Core.CreateMetrics();
        return telemetry.WithMetrics(
            metrics,
            session.CurrentTopology.Version.Value - initialTopologyVersion.Value + 1);
    }

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
