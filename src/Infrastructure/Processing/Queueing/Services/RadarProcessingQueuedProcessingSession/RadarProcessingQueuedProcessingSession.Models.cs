using System.Buffers;
using System.Diagnostics;
using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingQueuedProcessingSession
{
    private sealed class OrderedConcurrentBatchWork
    {
        public OrderedConcurrentBatchWork(
            Task<OrderedConcurrentBatchCompletion> task)
        {
            Task = task ?? throw new ArgumentNullException(nameof(task));
        }

        public Task<OrderedConcurrentBatchCompletion> Task { get; }
    }

    private sealed class OrderedConcurrentBatchCompletion : IDisposable
    {
        private readonly IDisposable? lease;
        private RadarProcessingBatchDelta? delta;
        private RadarProcessingAsyncBatchDeltaResult? asyncDelta;
        private IReadOnlyList<RadarProcessingHandlerDelta>? handlerDeltas;
        private RadarProcessingWorkerTelemetrySummary? workerTelemetry;
        private RadarProcessingQueuedBatchProcessingResult? processingResult;
        private bool disposed;

        private OrderedConcurrentBatchCompletion(
            RadarProcessingQueuedBatchSequence sequence,
            RadarProcessingBatchDelta? delta,
            RadarProcessingAsyncBatchDeltaResult? asyncDelta,
            IReadOnlyList<RadarProcessingHandlerDelta>? handlerDeltas,
            RadarProcessingWorkerTelemetrySummary? workerTelemetry,
            RadarProcessingQueuedBatchProcessingResult? processingResult,
            IDisposable? lease)
        {
            Sequence = sequence;
            this.delta = delta;
            this.asyncDelta = asyncDelta;
            this.handlerDeltas = handlerDeltas;
            this.workerTelemetry = workerTelemetry;
            this.processingResult = processingResult;
            this.lease = lease;
        }

        public RadarProcessingQueuedBatchSequence Sequence { get; }

        public static OrderedConcurrentBatchCompletion FromDelta(
            RadarProcessingQueuedBatchSequence sequence,
            RadarProcessingBatchDelta delta,
            IDisposable? lease)
        {
            ArgumentNullException.ThrowIfNull(delta);
            return new OrderedConcurrentBatchCompletion(sequence, delta, null, null, null, null, lease);
        }

        public static OrderedConcurrentBatchCompletion FromAsyncDelta(
            RadarProcessingQueuedBatchSequence sequence,
            RadarProcessingAsyncBatchDeltaResult asyncDelta,
            IDisposable? lease)
        {
            ArgumentNullException.ThrowIfNull(asyncDelta);
            return new OrderedConcurrentBatchCompletion(
                sequence,
                asyncDelta.Delta,
                asyncDelta,
                null,
                asyncDelta.WorkerTelemetry,
                null,
                lease);
        }

        public static OrderedConcurrentBatchCompletion FromHandlerDeltas(
            RadarProcessingQueuedBatchSequence sequence,
            RadarProcessingBatchDelta delta,
            IReadOnlyList<RadarProcessingHandlerDelta> handlerDeltas,
            IDisposable? lease)
        {
            ArgumentNullException.ThrowIfNull(delta);
            ArgumentNullException.ThrowIfNull(handlerDeltas);
            return new OrderedConcurrentBatchCompletion(
                sequence,
                delta,
                null,
                handlerDeltas,
                null,
                null,
                lease);
        }

        public static OrderedConcurrentBatchCompletion FromProcessingResult(
            RadarProcessingQueuedBatchProcessingResult processingResult,
            IDisposable? lease = null,
            bool leaseAlreadyDisposed = false)
        {
            ArgumentNullException.ThrowIfNull(processingResult);
            return new OrderedConcurrentBatchCompletion(
                processingResult.Sequence,
                null,
                null,
                null,
                null,
                processingResult,
                leaseAlreadyDisposed ? null : lease);
        }

        public RadarProcessingQueuedBatchProcessingResult Commit(
            RadarProcessingCore core,
            CancellationToken cancellationToken,
            IReadOnlyDictionary<string, RadarProcessingHandlerDeltaMergeCoordinator>? handlerMergeCoordinators = null)
        {
            ArgumentNullException.ThrowIfNull(core);
            if (processingResult is not null)
            {
                return processingResult;
            }

            if (delta is null)
            {
                throw new InvalidOperationException("Ordered concurrent completion has no delta or result.");
            }

            if (handlerDeltas is not null)
            {
                ArgumentNullException.ThrowIfNull(handlerMergeCoordinators);
                var invalid = core.ValidateProcessingDeltaForCommit(delta, cancellationToken);
                if (invalid is not null)
                {
                    processingResult = RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        Sequence,
                        invalid.Validation.Message,
                        invalid);
                    return processingResult;
                }

                List<IReadOnlyList<RadarProcessingHandlerDeltaValue>>? changedHandlerValueGroups = null;
                foreach (var handlerDelta in handlerDeltas)
                {
                    if (!handlerMergeCoordinators.TryGetValue(
                            handlerDelta.HandlerName,
                            out var coordinator))
                    {
                        processingResult = RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                            Sequence,
                            $"No merge coordinator exists for handler '{handlerDelta.HandlerName}'.");
                        return processingResult;
                    }

                    var mergeResult = coordinator.CompleteForCommit(handlerDelta);
                    if (mergeResult.IsRejected || mergeResult.IsBlocked)
                    {
                        processingResult = RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                            Sequence,
                            mergeResult.Message);
                        return processingResult;
                    }

                    if (mergeResult.AppliedValues.Count != 0)
                    {
                        changedHandlerValueGroups ??= new List<IReadOnlyList<RadarProcessingHandlerDeltaValue>>(
                            handlerDeltas.Count);
                        changedHandlerValueGroups.Add(mergeResult.AppliedValues);
                    }
                }

                var handlerResult = core.CommitValidatedProcessingDeltaWithMergedHandlerValueGroups(
                    delta,
                    changedHandlerValueGroups is null
                        ? Array.Empty<IReadOnlyList<RadarProcessingHandlerDeltaValue>>()
                        : changedHandlerValueGroups,
                    workerTelemetry,
                    cancellationToken);
                processingResult = handlerResult.IsValid
                    ? RadarProcessingQueuedBatchProcessingResult.Succeeded(Sequence, handlerResult)
                    : RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        Sequence,
                        handlerResult.Validation.Message,
                        handlerResult);
                return processingResult;
            }

            var result = core.CommitProcessingDelta(
                delta,
                workerTelemetry,
                cancellationToken);
            processingResult = result.IsValid
                ? RadarProcessingQueuedBatchProcessingResult.Succeeded(Sequence, result)
                : RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                    Sequence,
                    result.Validation.Message,
                    result);
            return processingResult;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            asyncDelta?.Dispose();
            if (asyncDelta is null)
            {
                delta?.Dispose();
            }

            asyncDelta = null;
            delta = null;
            handlerDeltas = null;
            workerTelemetry = null;
            lease?.Dispose();
        }
    }
}
