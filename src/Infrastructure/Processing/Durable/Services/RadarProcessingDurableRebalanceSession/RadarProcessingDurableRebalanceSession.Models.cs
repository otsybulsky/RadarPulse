using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingDurableRebalanceSession
{
    private sealed class DurableRebalanceCompletion : IDisposable
    {
        private readonly RadarEventBatch? batch;
        private RadarProcessingBatchDelta? delta;
        private RadarProcessingAsyncBatchDeltaResult? asyncDelta;
        private RadarProcessingWorkerTelemetrySummary? workerTelemetry;
        private RadarProcessingQueuedBatchProcessingResult? processingResult;
        private bool disposed;

        private DurableRebalanceCompletion(
            RadarProcessingDurableBatchId batchId,
            RadarProcessingQueuedBatchSequence sequence,
            RadarEventBatch? batch,
            RadarProcessingBatchDelta? delta,
            RadarProcessingAsyncBatchDeltaResult? asyncDelta,
            RadarProcessingWorkerTelemetrySummary? workerTelemetry,
            RadarProcessingQueuedBatchProcessingResult? processingResult)
        {
            BatchId = batchId;
            Sequence = sequence;
            this.batch = batch;
            this.delta = delta;
            this.asyncDelta = asyncDelta;
            this.workerTelemetry = workerTelemetry;
            this.processingResult = processingResult;
        }

        public RadarProcessingDurableBatchId BatchId { get; }

        public RadarProcessingQueuedBatchSequence Sequence { get; }

        public RadarProcessingQueuedBatchProcessingResult? ProcessingResult => processingResult;

        public static DurableRebalanceCompletion FromDelta(
            RadarProcessingDurableBatchId batchId,
            RadarProcessingQueuedBatchSequence sequence,
            RadarEventBatch batch,
            RadarProcessingBatchDelta delta)
        {
            ArgumentNullException.ThrowIfNull(batch);
            ArgumentNullException.ThrowIfNull(delta);
            return new DurableRebalanceCompletion(batchId, sequence, batch, delta, null, null, null);
        }

        public static DurableRebalanceCompletion FromAsyncDelta(
            RadarProcessingDurableBatchId batchId,
            RadarProcessingQueuedBatchSequence sequence,
            RadarEventBatch batch,
            RadarProcessingAsyncBatchDeltaResult asyncDelta)
        {
            ArgumentNullException.ThrowIfNull(batch);
            ArgumentNullException.ThrowIfNull(asyncDelta);
            return new DurableRebalanceCompletion(
                batchId,
                sequence,
                batch,
                asyncDelta.Delta,
                asyncDelta,
                asyncDelta.WorkerTelemetry,
                null);
        }

        public static DurableRebalanceCompletion FromProcessingResult(
            RadarProcessingDurableBatchId batchId,
            RadarProcessingQueuedBatchProcessingResult processingResult)
        {
            ArgumentNullException.ThrowIfNull(processingResult);
            return new DurableRebalanceCompletion(
                batchId,
                processingResult.Sequence,
                null,
                null,
                null,
                null,
                processingResult);
        }

        public RadarProcessingQueuedBatchProcessingResult Commit(
            RadarProcessingRebalanceSession rebalanceSession,
            RadarProcessingAsyncRebalanceSession? asyncRebalanceSession,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rebalanceSession);
            if (processingResult is not null)
            {
                return processingResult;
            }

            if (delta is null)
            {
                throw new InvalidOperationException("Durable rebalance completion has no delta or result.");
            }

            RecomputeStaleDeltaIfNeeded(
                rebalanceSession,
                asyncRebalanceSession,
                cancellationToken);

            var rebalanceResult = rebalanceSession.CommitProcessingDelta(
                delta,
                workerTelemetry,
                cancellationToken);
            processingResult = CreateProcessingResult(Sequence, rebalanceResult);
            return processingResult;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            DisposeCurrentDelta();
            workerTelemetry = null;
        }

        private void RecomputeStaleDeltaIfNeeded(
            RadarProcessingRebalanceSession rebalanceSession,
            RadarProcessingAsyncRebalanceSession? asyncRebalanceSession,
            CancellationToken cancellationToken)
        {
            if (delta is null ||
                delta.Route.TopologyVersion == rebalanceSession.CurrentTopology.Version)
            {
                return;
            }

            if (batch is null)
            {
                throw new InvalidOperationException("Durable rebalance stale topology recompute requires the source batch.");
            }

            DisposeCurrentDelta();
            if (asyncRebalanceSession is not null)
            {
                asyncDelta = asyncRebalanceSession
                    .AsyncCoreSession
                    .ComputeDeltaAsync(batch, cancellationToken)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
                delta = asyncDelta.Delta;
                workerTelemetry = asyncDelta.WorkerTelemetry;
                return;
            }

            delta = rebalanceSession.Core.ComputeProcessingDelta(batch, cancellationToken);
            workerTelemetry = null;
        }

        private void DisposeCurrentDelta()
        {
            asyncDelta?.Dispose();
            if (asyncDelta is null)
            {
                delta?.Dispose();
            }

            delta = null;
            asyncDelta = null;
        }

        private static RadarProcessingQueuedBatchProcessingResult CreateProcessingResult(
            RadarProcessingQueuedBatchSequence sequence,
            RadarProcessingRebalanceSessionResult rebalanceResult)
        {
            var processingResult = rebalanceResult.ProcessingResult;
            if (!processingResult.IsValid)
            {
                return RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                    sequence,
                    processingResult.Validation.Message,
                    processingResult);
            }

            if (rebalanceResult.MigrationResult is { Succeeded: false } migrationResult)
            {
                return RadarProcessingQueuedBatchProcessingResult.FailedMigration(
                    sequence,
                    $"Durable rebalance migration failed with state {migrationResult.State}.",
                    rebalanceResult);
            }

            if (!rebalanceResult.Validation.IsValid)
            {
                return RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                    sequence,
                    rebalanceResult.Validation.Message,
                    processingResult);
            }

            return RadarProcessingQueuedBatchProcessingResult.Succeeded(
                sequence,
                rebalanceResult);
        }
    }
}
