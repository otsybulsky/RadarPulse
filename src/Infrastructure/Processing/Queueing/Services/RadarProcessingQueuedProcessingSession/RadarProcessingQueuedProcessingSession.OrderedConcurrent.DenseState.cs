using System.Buffers;
using System.Diagnostics;
using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingQueuedProcessingSession
{
    private static void ApplyHandlersToDenseState(
        RadarEventBatch batch,
        RadarProcessingBatchDelta processingDelta,
        RadarSourceProcessingHandlerSlotLayout slotLayout,
        int[] sourceIndexById,
        long[] int64Slots,
        double[] doubleSlots,
        CancellationToken cancellationToken)
    {
        var events = batch.Events.Span;
        var payload = batch.Payload.Span;
        var routedEvents = processingDelta.Route.RoutedEvents.Span;
        var touchedSourceIds = processingDelta.TouchedSourceIds;
        for (var i = 0; i < routedEvents.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var routed = routedEvents[i];
            var streamEvent = events[routed.EventIndex];
            var denseSourceIndex = sourceIndexById[streamEvent.SourceId];
            if ((uint)denseSourceIndex >= (uint)touchedSourceIds.Length ||
                touchedSourceIds[denseSourceIndex] != streamEvent.SourceId)
            {
                throw new InvalidOperationException(
                    "Handler delta dense source map did not contain a routed source.");
            }

            var context = new RadarSourceProcessingHandlerContext(
                streamEvent,
                payload.Slice(streamEvent.PayloadOffset, streamEvent.PayloadLength),
                routed.PayloadMetrics);
            foreach (var assignment in slotLayout.Assignments)
            {
                assignment.Handler.Process(
                    context,
                    CreateDenseHandlerState(
                        denseSourceIndex,
                        assignment,
                        int64Slots,
                        doubleSlots,
                        slotLayout.TotalInt64SlotCount,
                        slotLayout.TotalDoubleSlotCount));
            }
        }
    }

    private static RadarSourceProcessingState CreateDenseHandlerState(
        int denseSourceIndex,
        RadarSourceProcessingHandlerSlotAssignment assignment,
        long[] int64Slots,
        double[] doubleSlots,
        int totalInt64SlotCount,
        int totalDoubleSlotCount)
    {
        var int64Span = assignment.Descriptor.Int64SlotCount == 0
            ? Span<long>.Empty
            : int64Slots.AsSpan(
                checked((denseSourceIndex * totalInt64SlotCount) + assignment.Int64SlotOffset),
                assignment.Descriptor.Int64SlotCount);
        var doubleSpan = assignment.Descriptor.DoubleSlotCount == 0
            ? Span<double>.Empty
            : doubleSlots.AsSpan(
                checked((denseSourceIndex * totalDoubleSlotCount) + assignment.DoubleSlotOffset),
                assignment.Descriptor.DoubleSlotCount);
        return new RadarSourceProcessingState(int64Span, doubleSpan);
    }

    private static RadarProcessingHandlerDeltaValue[] CreateHandlerDeltaValues(
        RadarSourceProcessingHandlerSlotAssignment assignment,
        ReadOnlySpan<int> touchedSourceIds,
        long[] int64Slots,
        double[] doubleSlots,
        int totalInt64SlotCount,
        int totalDoubleSlotCount)
    {
        var fields = assignment.Descriptor.SnapshotFields;
        if (fields.Count == 0 || touchedSourceIds.IsEmpty)
        {
            return [];
        }

        var values = new RadarProcessingHandlerDeltaValue[
            checked(touchedSourceIds.Length * fields.Count)];
        var valueIndex = 0;
        for (var denseSourceIndex = 0; denseSourceIndex < touchedSourceIds.Length; denseSourceIndex++)
        {
            var sourceId = touchedSourceIds[denseSourceIndex];
            foreach (var field in fields)
            {
                values[valueIndex++] = field.Type switch
                {
                    RadarSourceProcessingSnapshotFieldType.Int64 =>
                        RadarProcessingHandlerDeltaValue.ForInt64(
                            sourceId,
                            field.Name,
                            int64Slots[
                                checked((denseSourceIndex * totalInt64SlotCount) +
                                        assignment.Int64SlotOffset +
                                        field.SlotIndex)]),
                    RadarSourceProcessingSnapshotFieldType.Double =>
                        RadarProcessingHandlerDeltaValue.ForDouble(
                            sourceId,
                            field.Name,
                            doubleSlots[
                                checked((denseSourceIndex * totalDoubleSlotCount) +
                                        assignment.DoubleSlotOffset +
                                        field.SlotIndex)]),
                    _ => throw new InvalidOperationException("Unsupported handler snapshot field type.")
                };
            }
        }

        return values;
    }

}
