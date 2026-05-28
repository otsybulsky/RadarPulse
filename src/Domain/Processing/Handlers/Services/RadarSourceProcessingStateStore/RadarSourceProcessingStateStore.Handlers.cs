using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public sealed partial class RadarSourceProcessingStateStore
{
    private void ApplyHandlers(
        int sourceId,
        in RadarStreamEvent streamEvent,
        ReadOnlySpan<byte> eventPayload,
        RadarProcessingPayloadMetrics payloadMetrics)
    {
        if (!handlerSlotLayout.HasHandlers)
        {
            return;
        }

        var context = new RadarSourceProcessingHandlerContext(
            streamEvent,
            eventPayload,
            payloadMetrics);
        foreach (var assignment in handlerSlotLayout.Assignments)
        {
            assignment.Handler.Process(
                context,
                CreateHandlerState(sourceId, assignment));
        }
    }

    private RadarSourceProcessingState CreateHandlerState(
        int sourceId,
        RadarSourceProcessingHandlerSlotAssignment assignment)
    {
        var int64Slots = assignment.Descriptor.Int64SlotCount == 0
            ? Span<long>.Empty
            : handlerInt64Slots.AsSpan(
                GetSourceSlotOffset(sourceId, handlerSlotLayout.TotalInt64SlotCount, assignment.Int64SlotOffset),
                assignment.Descriptor.Int64SlotCount);
        var doubleSlots = assignment.Descriptor.DoubleSlotCount == 0
            ? Span<double>.Empty
            : handlerDoubleSlots.AsSpan(
                GetSourceSlotOffset(sourceId, handlerSlotLayout.TotalDoubleSlotCount, assignment.DoubleSlotOffset),
                assignment.Descriptor.DoubleSlotCount);

        return new RadarSourceProcessingState(int64Slots, doubleSlots);
    }

    private void ApplyMergedHandlerValue(
        RadarProcessingHandlerDeltaValue value)
    {
        foreach (var assignment in handlerSlotLayout.Assignments)
        {
            foreach (var field in assignment.Descriptor.SnapshotFields)
            {
                if (!string.Equals(field.Name, value.FieldName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (field.Type != value.Type)
                {
                    throw new ArgumentException(
                        "Merged handler value type must match the handler descriptor field type.",
                        nameof(value));
                }

                switch (value.Type)
                {
                    case RadarSourceProcessingSnapshotFieldType.Int64:
                        handlerInt64Slots[
                            GetSourceSlotOffset(
                                value.SourceId,
                                handlerSlotLayout.TotalInt64SlotCount,
                                assignment.Int64SlotOffset + field.SlotIndex)] = value.Int64Value;
                        return;

                    case RadarSourceProcessingSnapshotFieldType.Double:
                        handlerDoubleSlots[
                            GetSourceSlotOffset(
                                value.SourceId,
                                handlerSlotLayout.TotalDoubleSlotCount,
                                assignment.DoubleSlotOffset + field.SlotIndex)] = value.DoubleValue;
                        return;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(value));
                }
            }
        }

        throw new ArgumentException(
            $"Merged handler value field '{value.FieldName}' does not match any handler descriptor field.",
            nameof(value));
    }
}
