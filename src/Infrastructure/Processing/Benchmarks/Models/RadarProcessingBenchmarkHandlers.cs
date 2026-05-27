using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public static class RadarProcessingBenchmarkHandlers
{
    private const int HeavySampleCount = 16;
    private const ulong HeavyChecksumInitial = 14_695_981_039_346_656_037UL;
    private const ulong HeavyChecksumPrime = 1_099_511_628_211UL;

    public static IReadOnlyList<IRadarSourceProcessingHandler> Create(
        RadarProcessingBenchmarkHandlerSet handlerSet) =>
        handlerSet switch
        {
            RadarProcessingBenchmarkHandlerSet.None => Array.Empty<IRadarSourceProcessingHandler>(),
            RadarProcessingBenchmarkHandlerSet.CounterChecksum =>
                new IRadarSourceProcessingHandler[] { new CounterChecksumBenchmarkHandler() },
            RadarProcessingBenchmarkHandlerSet.CounterChecksumHeavy =>
                new IRadarSourceProcessingHandler[]
                {
                    new CounterChecksumBenchmarkHandler(),
                    new HeavySampledChecksumBenchmarkHandler()
                },
            _ => throw new ArgumentOutOfRangeException(nameof(handlerSet))
        };

    public static void EnsureKnown(
        RadarProcessingBenchmarkHandlerSet handlerSet)
    {
        if (handlerSet is not RadarProcessingBenchmarkHandlerSet.None and
            not RadarProcessingBenchmarkHandlerSet.CounterChecksum and
            not RadarProcessingBenchmarkHandlerSet.CounterChecksumHeavy)
        {
            throw new ArgumentOutOfRangeException(nameof(handlerSet));
        }
    }

    private static IReadOnlyList<RadarProcessingHandlerDeltaValue> MergeInt64Values(
        IReadOnlyList<RadarProcessingHandlerDeltaValue> currentValues,
        RadarProcessingHandlerDelta delta)
    {
        var values = currentValues.ToDictionary(
            static value => (value.SourceId, value.FieldName),
            static value => value.Int64Value);
        foreach (var value in delta.Values)
        {
            if (value.Type != RadarSourceProcessingSnapshotFieldType.Int64)
            {
                throw new ArgumentException("Benchmark handlers only support int64 handler delta values.");
            }

            var key = (value.SourceId, value.FieldName);
            values[key] = checked(values.GetValueOrDefault(key) + value.Int64Value);
        }

        return values
            .OrderBy(static pair => pair.Key.SourceId)
            .ThenBy(static pair => pair.Key.FieldName, StringComparer.Ordinal)
            .Select(static pair => RadarProcessingHandlerDeltaValue.ForInt64(
                pair.Key.SourceId,
                pair.Key.FieldName,
                pair.Value))
            .ToArray();
    }

    private sealed class CounterChecksumBenchmarkHandler :
        IRadarSourceProcessingHandler,
        IRadarSourceProcessingHandlerExecutionMetadata,
        IRadarProcessingHandlerDeltaMerger,
        IRadarProcessingHandlerDeltaAccumulatorFactory
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

        public RadarSourceProcessingHandlerExecutionClassification ExecutionClassification =>
            RadarSourceProcessingHandlerExecutionClassification.Mergeable;

        public string HandlerName => "benchmark.counter_checksum";

        public string HandlerContractVersion => "v1";

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            state.AddInt64(slotIndex: 0, value: 1);
            state.AddInt64(slotIndex: 1, context.PayloadMetrics.PayloadValueCount);
            state.AddInt64(slotIndex: 2, context.PayloadMetrics.RawValueChecksum);
        }

        public IReadOnlyList<RadarProcessingHandlerDeltaValue> Merge(
            IReadOnlyList<RadarProcessingHandlerDeltaValue> currentValues,
            RadarProcessingHandlerDelta delta) =>
            MergeInt64Values(currentValues, delta);

        public IRadarProcessingHandlerDeltaAccumulator CreateAccumulator() =>
            new Int64SumHandlerDeltaAccumulator();
    }

    private sealed class HeavySampledChecksumBenchmarkHandler :
        IRadarSourceProcessingHandler,
        IRadarSourceProcessingHandlerExecutionMetadata,
        IRadarProcessingHandlerDeltaMerger,
        IRadarProcessingHandlerDeltaAccumulatorFactory
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "benchmark.heavy_sampled_checksum",
                int64SlotCount: 3,
                doubleSlotCount: 0,
                new[]
                {
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "benchmark.heavy.events",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 0),
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "benchmark.heavy.payload_values",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 1),
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "benchmark.heavy.work_checksum",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 2)
                });

        public RadarSourceProcessingHandlerExecutionClassification ExecutionClassification =>
            RadarSourceProcessingHandlerExecutionClassification.Mergeable;

        public string HandlerName => "benchmark.heavy_sampled_checksum";

        public string HandlerContractVersion => "v1";

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            state.AddInt64(slotIndex: 0, value: 1);
            state.AddInt64(slotIndex: 1, context.PayloadMetrics.PayloadValueCount);
            state.AddInt64(slotIndex: 2, ComputeBoundedWorkChecksum(context));
        }

        public IReadOnlyList<RadarProcessingHandlerDeltaValue> Merge(
            IReadOnlyList<RadarProcessingHandlerDeltaValue> currentValues,
            RadarProcessingHandlerDelta delta) =>
            MergeInt64Values(currentValues, delta);

        public IRadarProcessingHandlerDeltaAccumulator CreateAccumulator() =>
            new Int64SumHandlerDeltaAccumulator();

        private static long ComputeBoundedWorkChecksum(
            in RadarSourceProcessingHandlerContext context)
        {
            var payload = context.Payload;
            var checksum = HeavyChecksumInitial;
            checksum = AppendInt64(checksum, context.StreamEvent.MessageTimestampUtcTicks);
            checksum = AppendInt64(checksum, context.StreamEvent.VolumeTimestampUtcTicks);
            checksum = AppendInt32(checksum, context.StreamEvent.RadialSequence);
            checksum = AppendInt64(checksum, context.PayloadMetrics.PayloadValueCount);
            checksum = AppendInt64(checksum, context.PayloadMetrics.RawValueChecksum);

            if (!payload.IsEmpty)
            {
                var step = Math.Max(1, payload.Length / HeavySampleCount);
                for (var sample = 0; sample < HeavySampleCount; sample++)
                {
                    var index = Math.Min(payload.Length - 1, sample * step);
                    checksum = AppendByte(checksum, payload[index]);
                    checksum = AppendByte(checksum, payload[payload.Length - 1 - index]);
                }
            }

            return (long)(checksum & 0x0000_0000_000f_ffffUL);
        }

        private static ulong AppendByte(ulong checksum, byte value) =>
            unchecked((checksum ^ value) * HeavyChecksumPrime);

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
    }

    private sealed class Int64SumHandlerDeltaAccumulator : IRadarProcessingHandlerDeltaAccumulator
    {
        private readonly Dictionary<(int SourceId, string FieldName), long> values = new();
        private readonly ReusableValueList changedValues = new();
        private RadarProcessingHandlerDeltaValue[] changedBuffer = [];

        public IReadOnlyList<RadarProcessingHandlerDeltaValue> Merge(
            RadarProcessingHandlerDelta delta)
        {
            ArgumentNullException.ThrowIfNull(delta);

            if (delta.Values.Count == 0)
            {
                return Array.Empty<RadarProcessingHandlerDeltaValue>();
            }

            if (changedBuffer.Length < delta.Values.Count)
            {
                changedBuffer = new RadarProcessingHandlerDeltaValue[delta.Values.Count];
            }

            for (var i = 0; i < delta.Values.Count; i++)
            {
                var value = delta.Values[i];
                if (value.Type != RadarSourceProcessingSnapshotFieldType.Int64)
                {
                    throw new ArgumentException("Benchmark handlers only support int64 handler delta values.");
                }

                var key = (value.SourceId, value.FieldName);
                var next = checked(values.GetValueOrDefault(key) + value.Int64Value);
                values[key] = next;
                changedBuffer[i] = RadarProcessingHandlerDeltaValue.ForInt64(
                    value.SourceId,
                    value.FieldName,
                    next);
            }

            changedValues.Reset(changedBuffer, delta.Values.Count);
            return changedValues;
        }

        public IReadOnlyList<RadarProcessingHandlerDeltaValue> CreateMergedValuesSnapshot() =>
            values
                .OrderBy(static pair => pair.Key.SourceId)
                .ThenBy(static pair => pair.Key.FieldName, StringComparer.Ordinal)
                .Select(static pair => RadarProcessingHandlerDeltaValue.ForInt64(
                    pair.Key.SourceId,
                    pair.Key.FieldName,
                    pair.Value))
                .ToArray();

        private sealed class ReusableValueList : IReadOnlyList<RadarProcessingHandlerDeltaValue>
        {
            private RadarProcessingHandlerDeltaValue[] values = [];

            public int Count { get; private set; }

            public RadarProcessingHandlerDeltaValue this[int index]
            {
                get
                {
                    if ((uint)index >= (uint)Count)
                    {
                        throw new ArgumentOutOfRangeException(nameof(index));
                    }

                    return values[index];
                }
            }

            public void Reset(
                RadarProcessingHandlerDeltaValue[] nextValues,
                int count)
            {
                ArgumentNullException.ThrowIfNull(nextValues);
                ArgumentOutOfRangeException.ThrowIfNegative(count);
                if (count > nextValues.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(count));
                }

                values = nextValues;
                Count = count;
            }

            public IEnumerator<RadarProcessingHandlerDeltaValue> GetEnumerator()
            {
                for (var i = 0; i < Count; i++)
                {
                    yield return values[i];
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
                GetEnumerator();
        }
    }
}
