using System.Buffers;
using System.Diagnostics;
using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingQueuedProcessingSession
{
    private IReadOnlyList<RadarProcessingHandlerDelta> CreateHandlerDeltas(
        RadarProcessingQueuedBatch queuedBatch,
        RadarProcessingBatchDelta processingDelta,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processingDelta);

        var contract = RadarProcessingHandlerOutputContract.FromOptions(core.Options);
        if (!contract.AllowsOrderedConcurrentHandlerDeltaMerge)
        {
            throw new NotSupportedException(
                "Ordered handler delta/merge requires all handlers to be mergeable.");
        }

        var handlerDeltaValues = CreateHandlerDeltaValues(
            queuedBatch.Batch,
            processingDelta,
            core.Options,
            cancellationToken);
        var result = new RadarProcessingHandlerDelta[contract.Handlers.Count];
        for (var handlerIndex = 0; handlerIndex < contract.Handlers.Count; handlerIndex++)
        {
            var descriptor = contract.Handlers[handlerIndex];
            if (core.Options.Handlers[descriptor.HandlerIndex] is not IRadarProcessingHandlerDeltaMerger merger)
            {
                throw new NotSupportedException(
                    $"Mergeable handler '{descriptor.Name}' must implement the handler delta merger contract.");
            }

            if (!string.Equals(merger.HandlerName, descriptor.Name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Mergeable handler '{descriptor.Name}' merger name does not match its descriptor.");
            }

            result[handlerIndex] = RadarProcessingHandlerDelta.CreateWithOwnedValues(
                descriptor.Name,
                merger.HandlerContractVersion,
                queuedBatch.Sequence,
                durableBatchId: null,
                queuedBatch.StreamEventCount,
                core.SourceCount,
                queuedBatch.PayloadValueCount,
                queuedBatch.RawValueChecksum,
                handlerDeltaValues[descriptor.HandlerIndex]);
        }

        return Array.AsReadOnly(result);
    }

    private static RadarProcessingHandlerDeltaValue[][] CreateHandlerDeltaValues(
        RadarEventBatch batch,
        RadarProcessingBatchDelta processingDelta,
        RadarProcessingCoreOptions options,
        CancellationToken cancellationToken)
    {
        var slotLayout = options.HandlerSlotLayout;
        var touchedSourceIds = processingDelta.TouchedSourceIds;
        var result = new RadarProcessingHandlerDeltaValue[slotLayout.Assignments.Count][];
        if (slotLayout.Assignments.Count == 0)
        {
            return result;
        }

        var sourceIndexById = ArrayPool<int>.Shared.Rent(processingDelta.SourceCount);
        var int64Slots = slotLayout.TotalInt64SlotCount == 0
            ? Array.Empty<long>()
            : new long[checked(touchedSourceIds.Length * slotLayout.TotalInt64SlotCount)];
        var doubleSlots = slotLayout.TotalDoubleSlotCount == 0
            ? Array.Empty<double>()
            : new double[checked(touchedSourceIds.Length * slotLayout.TotalDoubleSlotCount)];

        try
        {
            for (var i = 0; i < touchedSourceIds.Length; i++)
            {
                sourceIndexById[touchedSourceIds[i]] = i;
            }

            ApplyHandlersToDenseState(
                batch,
                processingDelta,
                slotLayout,
                sourceIndexById,
                int64Slots,
                doubleSlots,
                cancellationToken);

            foreach (var assignment in slotLayout.Assignments)
            {
                result[assignment.HandlerIndex] = CreateHandlerDeltaValues(
                    assignment,
                    touchedSourceIds,
                    int64Slots,
                    doubleSlots,
                    slotLayout.TotalInt64SlotCount,
                    slotLayout.TotalDoubleSlotCount);
            }
        }
        finally
        {
            for (var i = 0; i < touchedSourceIds.Length; i++)
            {
                sourceIndexById[touchedSourceIds[i]] = 0;
            }

            ArrayPool<int>.Shared.Return(sourceIndexById);
        }

        return result;
    }

}
