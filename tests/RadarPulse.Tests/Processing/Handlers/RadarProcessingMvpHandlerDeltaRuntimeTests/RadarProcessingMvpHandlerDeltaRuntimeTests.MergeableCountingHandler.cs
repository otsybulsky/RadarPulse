using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingMvpHandlerDeltaRuntimeTests
{
    private sealed class MergeableCountingHandler :
        IRadarSourceProcessingHandler,
        IRadarSourceProcessingHandlerExecutionMetadata,
        IRadarProcessingHandlerDeltaMerger
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "counting",
                int64SlotCount: 1,
                doubleSlotCount: 0,
                new[]
                {
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "events",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 0)
                });

        public RadarSourceProcessingHandlerExecutionClassification ExecutionClassification =>
            RadarSourceProcessingHandlerExecutionClassification.Mergeable;

        public string HandlerName => "counting";

        public string HandlerContractVersion => "v1";

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            state.AddInt64(slotIndex: 0, value: 1);
        }

        public IReadOnlyList<RadarProcessingHandlerDeltaValue> Merge(
            IReadOnlyList<RadarProcessingHandlerDeltaValue> currentValues,
            RadarProcessingHandlerDelta delta)
        {
            var values = currentValues.ToDictionary(
                static value => value.SourceId,
                static value => value.Int64Value);
            foreach (var value in delta.Values)
            {
                values[value.SourceId] = values.GetValueOrDefault(value.SourceId) + value.Int64Value;
            }

            return values
                .OrderBy(static pair => pair.Key)
                .Select(static pair => RadarProcessingHandlerDeltaValue.ForInt64(
                    pair.Key,
                    "events",
                    pair.Value))
                .ToArray();
        }
    }
}
