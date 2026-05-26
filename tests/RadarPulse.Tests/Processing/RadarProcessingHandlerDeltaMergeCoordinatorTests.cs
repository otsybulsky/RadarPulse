using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingHandlerDeltaMergeCoordinatorTests
{
    [Fact]
    public void OutOfOrderCompletedDeltasMergeInProviderSequence()
    {
        var coordinator = new RadarProcessingHandlerDeltaMergeCoordinator(new SummingMerger());

        var later = coordinator.Complete(CreateDelta(sequence: 1, sourceId: 0, events: 2));
        Assert.True(later.IsAccepted);
        Assert.Equal(0, later.AppliedDeltaCount);
        Assert.True(later.Summary.IsBlocked);
        Assert.Equal(new RadarProcessingQueuedBatchSequence(0), later.Summary.FirstBlockingSequence);

        var earlier = coordinator.Complete(CreateDelta(sequence: 0, sourceId: 0, events: 1));

        Assert.True(earlier.IsAccepted);
        Assert.Equal(2, earlier.AppliedDeltaCount);
        Assert.True(earlier.Summary.IsReady);
        Assert.Equal(new RadarProcessingQueuedBatchSequence(2), earlier.Summary.NextProviderSequence);
        var output = Assert.Single(earlier.Summary.MergedValues);
        Assert.Equal(0, output.SourceId);
        Assert.Equal("events", output.FieldName);
        Assert.Equal(3, output.Int64Value);
    }

    [Fact]
    public void LaterCompletedDeltaWaitsBehindMissingEarlierSequence()
    {
        var coordinator = new RadarProcessingHandlerDeltaMergeCoordinator(new SummingMerger());

        var result = coordinator.Complete(CreateDelta(sequence: 2, sourceId: 0, events: 1));

        Assert.True(result.IsAccepted);
        Assert.Equal(0, result.AppliedDeltaCount);
        Assert.Equal(1, result.Summary.PendingDeltaCount);
        Assert.Equal(new RadarProcessingQueuedBatchSequence(0), result.Summary.FirstBlockingSequence);
        Assert.Contains("Waiting for handler delta", result.Summary.FirstBlockingReason, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateDeltaApplicationDoesNotDoubleCountOutput()
    {
        var coordinator = new RadarProcessingHandlerDeltaMergeCoordinator(new SummingMerger());
        var delta = CreateDelta(sequence: 0, sourceId: 0, events: 5);

        var first = coordinator.Complete(delta);
        var duplicate = coordinator.Complete(delta);

        Assert.True(first.IsAccepted);
        Assert.True(duplicate.IsDuplicate);
        Assert.Equal(0, duplicate.AppliedDeltaCount);
        var output = Assert.Single(duplicate.Summary.MergedValues);
        Assert.Equal(5, output.Int64Value);
        Assert.Equal(1, duplicate.Summary.AppliedDeltaCount);
    }

    [Fact]
    public void InvalidEarlierDeltaBlocksLaterMerge()
    {
        var coordinator = new RadarProcessingHandlerDeltaMergeCoordinator(new SummingMerger());

        var rejected = coordinator.Complete(CreateDelta(sequence: 0, sourceId: 0, events: 1, handlerName: "wrong"));
        var later = coordinator.Complete(CreateDelta(sequence: 1, sourceId: 0, events: 1));

        Assert.True(rejected.IsRejected);
        Assert.Equal(new RadarProcessingQueuedBatchSequence(0), rejected.Summary.FirstBlockingSequence);
        Assert.Contains("does not match merger", rejected.Message, StringComparison.Ordinal);
        Assert.True(later.IsBlocked);
        Assert.Equal(0, later.Summary.AppliedDeltaCount);
        Assert.Empty(later.Summary.MergedValues);
    }

    [Fact]
    public void MergedOutputMatchesSequentialFallbackForSameInputBatches()
    {
        var coordinator = new RadarProcessingHandlerDeltaMergeCoordinator(new SummingMerger());
        var expected = new Dictionary<int, long>
        {
            [0] = 0,
            [1] = 0
        };

        foreach (var delta in new[]
                 {
                     CreateDelta(sequence: 0, sourceId: 0, events: 2),
                     CreateDelta(sequence: 1, sourceId: 1, events: 3),
                     CreateDelta(sequence: 2, sourceId: 0, events: 4)
                 })
        {
            expected[delta.Values[0].SourceId] += delta.Values[0].Int64Value;
            coordinator.Complete(delta);
        }

        var actual = coordinator.CreateSummary().MergedValues
            .OrderBy(static value => value.SourceId)
            .ToArray();

        Assert.Equal(2, actual.Length);
        Assert.Equal(expected[0], actual[0].Int64Value);
        Assert.Equal(expected[1], actual[1].Int64Value);
    }

    [Fact]
    public void AccumulatorMergeReturnsChangedValuesAndKeepsFullSummary()
    {
        var coordinator = new RadarProcessingHandlerDeltaMergeCoordinator(new AccumulatingSummingMerger());

        var first = coordinator.Complete(CreateDelta(sequence: 0, sourceId: 0, events: 2));
        var second = coordinator.Complete(CreateDelta(sequence: 1, sourceId: 0, events: 3));

        Assert.True(first.IsAccepted);
        Assert.Equal(2, Assert.Single(first.AppliedValues).Int64Value);
        Assert.Equal(2, Assert.Single(first.Summary.MergedValues).Int64Value);
        Assert.True(second.IsAccepted);
        Assert.Equal(5, Assert.Single(second.AppliedValues).Int64Value);
        Assert.Equal(5, Assert.Single(second.Summary.MergedValues).Int64Value);
    }

    [Fact]
    public void SummaryDoesNotExposeMutableCoordinatorState()
    {
        var coordinator = new RadarProcessingHandlerDeltaMergeCoordinator(new SummingMerger());
        coordinator.Complete(CreateDelta(sequence: 0, sourceId: 0, events: 1));

        var first = coordinator.CreateSummary();
        coordinator.Complete(CreateDelta(sequence: 1, sourceId: 0, events: 1));
        var second = coordinator.CreateSummary();

        Assert.Equal(1, Assert.Single(first.MergedValues).Int64Value);
        Assert.Equal(2, Assert.Single(second.MergedValues).Int64Value);
    }

    private static RadarProcessingHandlerDelta CreateDelta(
        long sequence,
        int sourceId,
        long events,
        string handlerName = "analytics") =>
        RadarProcessingHandlerDelta.Create(
            handlerName,
            "v1",
            new RadarProcessingQueuedBatchSequence(sequence),
            durableBatchId: null,
            eventCount: checked((int)events),
            sourceCount: 2,
            payloadValueCount: events,
            inputChecksum: sequence + events + sourceId,
            values:
            [
                RadarProcessingHandlerDeltaValue.ForInt64(sourceId, "events", events)
            ]);

    private sealed class SummingMerger : IRadarProcessingHandlerDeltaMerger
    {
        public string HandlerName => "analytics";

        public string HandlerContractVersion => "v1";

        public IReadOnlyList<RadarProcessingHandlerDeltaValue> Merge(
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
                    throw new ArgumentException("Only int64 values are supported by this test merger.");
                }

                var key = (value.SourceId, value.FieldName);
                values[key] = values.GetValueOrDefault(key) + value.Int64Value;
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
    }

    private sealed class AccumulatingSummingMerger :
        IRadarProcessingHandlerDeltaMerger,
        IRadarProcessingHandlerDeltaAccumulatorFactory
    {
        public string HandlerName => "analytics";

        public string HandlerContractVersion => "v1";

        public IReadOnlyList<RadarProcessingHandlerDeltaValue> Merge(
            IReadOnlyList<RadarProcessingHandlerDeltaValue> currentValues,
            RadarProcessingHandlerDelta delta) =>
            new SummingMerger().Merge(currentValues, delta);

        public IRadarProcessingHandlerDeltaAccumulator CreateAccumulator() =>
            new Accumulator();

        private sealed class Accumulator : IRadarProcessingHandlerDeltaAccumulator
        {
            private readonly Dictionary<(int SourceId, string FieldName), long> values = new();

            public IReadOnlyList<RadarProcessingHandlerDeltaValue> Merge(
                RadarProcessingHandlerDelta delta)
            {
                var changed = new RadarProcessingHandlerDeltaValue[delta.Values.Count];
                for (var i = 0; i < delta.Values.Count; i++)
                {
                    var value = delta.Values[i];
                    var key = (value.SourceId, value.FieldName);
                    var next = values.GetValueOrDefault(key) + value.Int64Value;
                    values[key] = next;
                    changed[i] = RadarProcessingHandlerDeltaValue.ForInt64(
                        value.SourceId,
                        value.FieldName,
                        next);
                }

                return changed;
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
        }
    }
}
