using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingDurableProcessingSession
{
    private sealed class DurableProcessingCompletion : IDisposable
    {
        private RadarProcessingBatchDelta? delta;
        private RadarProcessingAsyncBatchDeltaResult? asyncDelta;
        private RadarProcessingWorkerTelemetrySummary? workerTelemetry;
        private RadarProcessingQueuedBatchProcessingResult? processingResult;
        private bool disposed;

        private DurableProcessingCompletion(
            RadarProcessingDurableBatchId batchId,
            RadarProcessingQueuedBatchSequence sequence,
            RadarProcessingBatchDelta? delta,
            RadarProcessingAsyncBatchDeltaResult? asyncDelta,
            RadarProcessingWorkerTelemetrySummary? workerTelemetry,
            RadarProcessingQueuedBatchProcessingResult? processingResult)
        {
            BatchId = batchId;
            Sequence = sequence;
            this.delta = delta;
            this.asyncDelta = asyncDelta;
            this.workerTelemetry = workerTelemetry;
            this.processingResult = processingResult;
        }

        public RadarProcessingDurableBatchId BatchId { get; }

        public RadarProcessingQueuedBatchSequence Sequence { get; }

        public RadarProcessingQueuedBatchProcessingResult? ProcessingResult => processingResult;

        public static DurableProcessingCompletion FromDelta(
            RadarProcessingDurableBatchId batchId,
            RadarProcessingQueuedBatchSequence sequence,
            RadarProcessingBatchDelta delta)
        {
            ArgumentNullException.ThrowIfNull(delta);
            return new DurableProcessingCompletion(batchId, sequence, delta, null, null, null);
        }

        public static DurableProcessingCompletion FromAsyncDelta(
            RadarProcessingDurableBatchId batchId,
            RadarProcessingQueuedBatchSequence sequence,
            RadarProcessingAsyncBatchDeltaResult asyncDelta)
        {
            ArgumentNullException.ThrowIfNull(asyncDelta);
            return new DurableProcessingCompletion(
                batchId,
                sequence,
                asyncDelta.Delta,
                asyncDelta,
                asyncDelta.WorkerTelemetry,
                null);
        }

        public static DurableProcessingCompletion FromProcessingResult(
            RadarProcessingDurableBatchId batchId,
            RadarProcessingQueuedBatchProcessingResult processingResult)
        {
            ArgumentNullException.ThrowIfNull(processingResult);
            return new DurableProcessingCompletion(
                batchId,
                processingResult.Sequence,
                null,
                null,
                null,
                processingResult);
        }

        public RadarProcessingQueuedBatchProcessingResult Commit(
            RadarProcessingCore core,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(core);
            if (processingResult is not null)
            {
                return processingResult;
            }

            if (delta is null)
            {
                throw new InvalidOperationException("Durable processing completion has no delta or result.");
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

            delta = null;
            asyncDelta = null;
            workerTelemetry = null;
        }
    }
}
