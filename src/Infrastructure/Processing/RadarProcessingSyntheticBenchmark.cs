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
        CancellationToken cancellationToken)
    {
        var workload = RadarProcessingSyntheticWorkload.Create(workloadOptions);
        return Measure(
            workload,
            executionMode,
            partitionCount,
            shardCount,
            handlerSet,
            iterations,
            warmupIterations,
            cancellationToken);
    }

    public RadarProcessingBenchmarkResult Measure(
        RadarProcessingSyntheticWorkload workload,
        RadarProcessingExecutionMode executionMode,
        int partitionCount,
        int shardCount,
        RadarProcessingBenchmarkHandlerSet handlerSet,
        int iterations,
        int warmupIterations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workload);
        EnsureKnownExecutionMode(executionMode);
        EnsureKnownHandlerSet(handlerSet);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);
        ArgumentOutOfRangeException.ThrowIfNegative(warmupIterations);

        var coreOptions = CreateCoreOptions(executionMode, partitionCount, shardCount, handlerSet);
        var shardDistributions = CreateShardDistributions(workload, coreOptions);
        var validationChecksum = ComputeValidationChecksum(workload, coreOptions, cancellationToken);
        var core = new RadarProcessingCore(workload.SourceUniverse, coreOptions);

        for (var warmupIteration = 0; warmupIteration < warmupIterations; warmupIteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProcessAndValidateIteration(core, workload, coreOptions, expectedIteration: null, cancellationToken);
        }

        var allocatedBytesBefore = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        IterationTotals? expectedIteration = null;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var iterationTotals = ProcessAndValidateIteration(
                core,
                workload,
                coreOptions,
                expectedIteration,
                cancellationToken);
            expectedIteration ??= iterationTotals;
        }

        stopwatch.Stop();
        var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBytesBefore;
        var measuredIteration = expectedIteration ??
                                throw new InvalidOperationException("Processing benchmark did not run any iterations.");

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
            validationChecksum,
            shardDistributions,
            stopwatch.Elapsed,
            allocatedBytes);
    }

    private static RadarProcessingCoreOptions CreateCoreOptions(
        RadarProcessingExecutionMode executionMode,
        int partitionCount,
        int shardCount,
        RadarProcessingBenchmarkHandlerSet handlerSet) =>
        new(
            executionMode,
            partitionCount,
            shardCount,
            enableValidation: true,
            CreateHandlers(handlerSet));

    private static IReadOnlyList<IRadarSourceProcessingHandler> CreateHandlers(
        RadarProcessingBenchmarkHandlerSet handlerSet) =>
        handlerSet switch
        {
            RadarProcessingBenchmarkHandlerSet.None => Array.Empty<IRadarSourceProcessingHandler>(),
            RadarProcessingBenchmarkHandlerSet.CounterChecksum =>
                new IRadarSourceProcessingHandler[] { new CounterChecksumBenchmarkHandler() },
            _ => throw new ArgumentOutOfRangeException(nameof(handlerSet))
        };

    private static IterationTotals ProcessAndValidateIteration(
        RadarProcessingCore core,
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
            var result = core.Process(batch, cancellationToken);
            if (!result.IsValid)
            {
                throw new InvalidDataException(result.Validation.Message);
            }

            if (coreOptions.ExecutionMode == RadarProcessingExecutionMode.PartitionedBarrier &&
                result.Telemetry is null)
            {
                throw new InvalidDataException("Partitioned processing benchmark did not produce telemetry.");
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

        return iterationTotals;
    }

    private static ulong ComputeValidationChecksum(
        RadarProcessingSyntheticWorkload workload,
        RadarProcessingCoreOptions coreOptions,
        CancellationToken cancellationToken)
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
        if (handlerSet is not RadarProcessingBenchmarkHandlerSet.None and
            not RadarProcessingBenchmarkHandlerSet.CounterChecksum)
        {
            throw new ArgumentOutOfRangeException(nameof(handlerSet));
        }
    }

    private static void EnsureKnownExecutionMode(RadarProcessingExecutionMode executionMode)
    {
        if (executionMode is not RadarProcessingExecutionMode.Sequential and
            not RadarProcessingExecutionMode.PartitionedBarrier)
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

    private sealed class CounterChecksumBenchmarkHandler : IRadarSourceProcessingHandler
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "benchmark.counter_checksum",
                int64SlotCount: 3,
                doubleSlotCount: 0,
                new[]
                {
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "benchmark.events",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 0),
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "benchmark.payload_values",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 1),
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "benchmark.raw_checksum",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 2)
                });

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            state.AddInt64(slotIndex: 0, value: 1);
            state.AddInt64(slotIndex: 1, context.PayloadMetrics.PayloadValueCount);
            state.AddInt64(slotIndex: 2, context.PayloadMetrics.RawValueChecksum);
        }
    }
}
