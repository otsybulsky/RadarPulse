using System.Diagnostics;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Processing;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingHandlerDeltaPerformanceGateTests
{
    private sealed class HandlerHeavySummaryHandler :
        IRadarSourceProcessingHandler,
        IRadarSourceProcessingHandlerExecutionMetadata,
        IRadarProcessingHandlerDeltaMerger
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "handler.heavy",
                int64SlotCount: 3,
                doubleSlotCount: 0,
                new[]
                {
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "events",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 0),
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "payload.values",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 1),
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "handler.work",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 2)
                });

        public RadarSourceProcessingHandlerExecutionClassification ExecutionClassification =>
            RadarSourceProcessingHandlerExecutionClassification.Mergeable;

        public string HandlerName => "handler.heavy";

        public string HandlerContractVersion => "v1";

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            var work = 0L;
            for (var repeat = 0; repeat < 96; repeat++)
            {
                for (var i = 0; i < context.Payload.Length; i++)
                {
                    work = checked(work + context.Payload[i] + repeat);
                }
            }

            state.AddInt64(slotIndex: 0, value: 1);
            state.AddInt64(slotIndex: 1, context.PayloadMetrics.PayloadValueCount);
            state.AddInt64(slotIndex: 2, work);
        }

        public IReadOnlyList<RadarProcessingHandlerDeltaValue> Merge(
            IReadOnlyList<RadarProcessingHandlerDeltaValue> currentValues,
            RadarProcessingHandlerDelta delta)
        {
            var values = currentValues.ToDictionary(
                static value => (value.SourceId, value.FieldName),
                static value => value.Int64Value);
            foreach (var value in delta.Values)
            {
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
}
