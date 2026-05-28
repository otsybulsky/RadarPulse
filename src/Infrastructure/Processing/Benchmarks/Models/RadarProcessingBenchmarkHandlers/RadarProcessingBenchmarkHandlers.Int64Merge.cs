using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public static partial class RadarProcessingBenchmarkHandlers
{
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
