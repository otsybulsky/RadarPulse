using System.Buffers;
using System.Diagnostics;
using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingQueuedProcessingSession
{
    private OrderedConcurrentBatchWork StartOrderedConcurrentBatch(
        RadarProcessingQueuedBatch queuedBatch,
        CancellationToken cancellationToken)
    {
        var lease = consumerResourceLeaseFactory?.Invoke(queuedBatch.Sequence);
        var task = Task.Run(
            () => ComputeOrderedConcurrentBatch(queuedBatch, lease, cancellationToken),
            CancellationToken.None);
        return new OrderedConcurrentBatchWork(task);
    }

    private OrderedConcurrentBatchWork StartOrderedConcurrentHandlerDeltaBatch(
        RadarProcessingQueuedBatch queuedBatch,
        CancellationToken cancellationToken)
    {
        var lease = consumerResourceLeaseFactory?.Invoke(queuedBatch.Sequence);
        var task = Task.Run(
            () => ComputeOrderedConcurrentHandlerDeltaBatch(queuedBatch, lease, cancellationToken),
            CancellationToken.None);
        return new OrderedConcurrentBatchWork(task);
    }

    private OrderedConcurrentBatchCompletion CreateSkippedAfterFaultCompletion(
        RadarProcessingQueuedBatch queuedBatch)
    {
        using var lease = consumerResourceLeaseFactory?.Invoke(queuedBatch.Sequence);
        return OrderedConcurrentBatchCompletion.FromProcessingResult(
            RadarProcessingQueuedBatchProcessingResult.SkippedAfterFault(
                queuedBatch.Sequence,
                faultMessage));
    }

    private OrderedConcurrentBatchCompletion ComputeOrderedConcurrentBatch(
        RadarProcessingQueuedBatch queuedBatch,
        IDisposable? lease,
        CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                lease?.Dispose();
                return OrderedConcurrentBatchCompletion.FromProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.Canceled(
                        queuedBatch.Sequence,
                        "Queued processing batch was canceled."),
                    leaseAlreadyDisposed: true);
            }

            var invalid = core.ValidateBatchForProcessing(queuedBatch.Batch, cancellationToken);
            if (invalid is not null)
            {
                return OrderedConcurrentBatchCompletion.FromProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        queuedBatch.Sequence,
                        invalid.Validation.Message,
                        invalid),
                    lease);
            }

            if (asyncCoreSession is not null)
            {
                var asyncDelta = asyncCoreSession
                    .ComputeDeltaAsync(queuedBatch.Batch, cancellationToken)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
                return OrderedConcurrentBatchCompletion.FromAsyncDelta(queuedBatch.Sequence, asyncDelta, lease);
            }

            var delta = core.ComputeProcessingDelta(queuedBatch.Batch, cancellationToken);
            return OrderedConcurrentBatchCompletion.FromDelta(queuedBatch.Sequence, delta, lease);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return OrderedConcurrentBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.Canceled(
                    queuedBatch.Sequence,
                    "Queued processing batch was canceled."),
                lease);
        }
        catch (RadarProcessingBatchDeltaValidationException exception)
        {
            var result = core.CreateInvalidProcessingResult(
                exception.Error,
                exception.SourceId,
                exception.EventIndex,
                exception.Message);
            return OrderedConcurrentBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                    queuedBatch.Sequence,
                    exception.Message,
                    result),
                lease);
        }
        catch (Exception exception)
        {
            return OrderedConcurrentBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                    queuedBatch.Sequence,
                    exception.Message),
                lease);
        }
    }

    private OrderedConcurrentBatchCompletion ComputeOrderedConcurrentHandlerDeltaBatch(
        RadarProcessingQueuedBatch queuedBatch,
        IDisposable? lease,
        CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                lease?.Dispose();
                return OrderedConcurrentBatchCompletion.FromProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.Canceled(
                        queuedBatch.Sequence,
                        "Queued processing batch was canceled."),
                    leaseAlreadyDisposed: true);
            }

            var invalid = core.ValidateBatchForProcessing(queuedBatch.Batch, cancellationToken);
            if (invalid is not null)
            {
                return OrderedConcurrentBatchCompletion.FromProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        queuedBatch.Sequence,
                        invalid.Validation.Message,
                        invalid),
                    lease);
            }

            var delta = core.ComputeProcessingDeltaForHandlerDeltaMerge(queuedBatch.Batch, cancellationToken);
            var handlerDeltas = CreateHandlerDeltas(queuedBatch, delta, cancellationToken);
            return OrderedConcurrentBatchCompletion.FromHandlerDeltas(
                queuedBatch.Sequence,
                delta,
                handlerDeltas,
                lease);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return OrderedConcurrentBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.Canceled(
                    queuedBatch.Sequence,
                    "Queued processing batch was canceled."),
                lease);
        }
        catch (RadarProcessingBatchDeltaValidationException exception)
        {
            var result = core.CreateInvalidProcessingResult(
                exception.Error,
                exception.SourceId,
                exception.EventIndex,
                exception.Message);
            return OrderedConcurrentBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                    queuedBatch.Sequence,
                    exception.Message,
                    result),
                lease);
        }
        catch (Exception exception)
        {
            return OrderedConcurrentBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                    queuedBatch.Sequence,
                    exception.Message),
                lease);
        }
    }

    private void PublishReadyOrderedCompletions(
        List<OrderedConcurrentBatchCompletion> completed,
        ref long nextPublishSequence,
        CancellationTokenSource activeCancellation)
    {
        if (nextPublishSequence < 0)
        {
            return;
        }

        while (true)
        {
            var index = FindCompletionIndex(completed, nextPublishSequence);
            if (index < 0)
            {
                return;
            }

            var completion = completed[index];
            completed.RemoveAt(index);
            try
            {
                var result = IsFaulted
                    ? RadarProcessingQueuedBatchProcessingResult.SkippedAfterFault(
                        completion.Sequence,
                        faultMessage)
                    : completion.Commit(core, activeCancellation.Token);
                RecordProcessingResult(result);
                nextPublishSequence++;

                if (IsFailedProcessingStatus(result.Status))
                {
                    MarkFaulted(result.Message);
                    activeCancellation.Cancel();
                }
                else if (result.Status == RadarProcessingQueuedBatchProcessingStatus.Canceled)
                {
                    activeCancellation.Cancel();
                    MarkCanceledAndRecordQueued();
                }
            }
            finally
            {
                completion.Dispose();
            }
        }
    }

    private void PublishReadyHandlerDeltaMergeCompletions(
        List<OrderedConcurrentBatchCompletion> completed,
        ref long nextPublishSequence,
        CancellationTokenSource activeCancellation,
        IReadOnlyDictionary<string, RadarProcessingHandlerDeltaMergeCoordinator> handlerMergeCoordinators)
    {
        if (nextPublishSequence < 0)
        {
            return;
        }

        while (true)
        {
            var index = FindCompletionIndex(completed, nextPublishSequence);
            if (index < 0)
            {
                return;
            }

            var completion = completed[index];
            completed.RemoveAt(index);
            try
            {
                var result = IsFaulted
                    ? RadarProcessingQueuedBatchProcessingResult.SkippedAfterFault(
                        completion.Sequence,
                        faultMessage)
                    : completion.Commit(core, activeCancellation.Token, handlerMergeCoordinators);
                RecordProcessingResult(result);
                nextPublishSequence++;

                if (IsFailedProcessingStatus(result.Status))
                {
                    MarkFaulted(result.Message);
                    activeCancellation.Cancel();
                }
                else if (result.Status == RadarProcessingQueuedBatchProcessingStatus.Canceled)
                {
                    activeCancellation.Cancel();
                    MarkCanceledAndRecordQueued();
                }
            }
            finally
            {
                completion.Dispose();
            }
        }
    }

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

    private static IReadOnlyDictionary<string, RadarProcessingHandlerDeltaMergeCoordinator> CreateHandlerDeltaMergeCoordinators(
        RadarProcessingCore core)
    {
        var contract = RadarProcessingHandlerOutputContract.FromOptions(core.Options);
        if (!contract.AllowsOrderedConcurrentHandlerDeltaMerge)
        {
            throw new NotSupportedException(
                "Ordered handler delta/merge requires a mergeable handler output contract.");
        }

        var result = new Dictionary<string, RadarProcessingHandlerDeltaMergeCoordinator>(StringComparer.Ordinal);
        foreach (var descriptor in contract.Handlers)
        {
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

            result.Add(
                descriptor.Name,
                new RadarProcessingHandlerDeltaMergeCoordinator(merger));
        }

        return result;
    }
}
