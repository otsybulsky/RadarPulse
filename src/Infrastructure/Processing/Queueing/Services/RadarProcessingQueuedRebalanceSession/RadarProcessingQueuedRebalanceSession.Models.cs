using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingQueuedRebalanceSession
{
    private sealed class OrderedConcurrentRebalanceBatchWork
    {
        public OrderedConcurrentRebalanceBatchWork(
            Task<OrderedConcurrentRebalanceBatchCompletion> task)
        {
            Task = task ?? throw new ArgumentNullException(nameof(task));
        }

        public Task<OrderedConcurrentRebalanceBatchCompletion> Task { get; }
    }

    private sealed class OrderedConcurrentRebalanceBatchCompletion : IDisposable
    {
        private readonly RadarEventBatch? batch;
        private readonly IDisposable? lease;
        private RadarProcessingBatchDelta? delta;
        private RadarProcessingAsyncBatchDeltaResult? asyncDelta;
        private RadarProcessingWorkerTelemetrySummary? workerTelemetry;
        private RadarProcessingQueuedBatchProcessingResult? processingResult;
        private bool disposed;

        private OrderedConcurrentRebalanceBatchCompletion(
            RadarProcessingQueuedBatchSequence sequence,
            RadarEventBatch? batch,
            RadarProcessingBatchDelta? delta,
            RadarProcessingAsyncBatchDeltaResult? asyncDelta,
            RadarProcessingWorkerTelemetrySummary? workerTelemetry,
            RadarProcessingQueuedBatchProcessingResult? processingResult,
            IDisposable? lease)
        {
            Sequence = sequence;
            this.batch = batch;
            this.delta = delta;
            this.asyncDelta = asyncDelta;
            this.workerTelemetry = workerTelemetry;
            this.processingResult = processingResult;
            this.lease = lease;
        }

        public RadarProcessingQueuedBatchSequence Sequence { get; }

        public static OrderedConcurrentRebalanceBatchCompletion FromDelta(
            RadarProcessingQueuedBatchSequence sequence,
            RadarEventBatch batch,
            RadarProcessingBatchDelta delta,
            IDisposable? lease)
        {
            ArgumentNullException.ThrowIfNull(batch);
            ArgumentNullException.ThrowIfNull(delta);
            return new OrderedConcurrentRebalanceBatchCompletion(sequence, batch, delta, null, null, null, lease);
        }

        public static OrderedConcurrentRebalanceBatchCompletion FromAsyncDelta(
            RadarProcessingQueuedBatchSequence sequence,
            RadarEventBatch batch,
            RadarProcessingAsyncBatchDeltaResult asyncDelta,
            IDisposable? lease)
        {
            ArgumentNullException.ThrowIfNull(batch);
            ArgumentNullException.ThrowIfNull(asyncDelta);
            return new OrderedConcurrentRebalanceBatchCompletion(
                sequence,
                batch,
                asyncDelta.Delta,
                asyncDelta,
                asyncDelta.WorkerTelemetry,
                null,
                lease);
        }

        public static OrderedConcurrentRebalanceBatchCompletion FromProcessingResult(
            RadarProcessingQueuedBatchProcessingResult processingResult,
            IDisposable? lease = null,
            bool leaseAlreadyDisposed = false)
        {
            ArgumentNullException.ThrowIfNull(processingResult);
            return new OrderedConcurrentRebalanceBatchCompletion(
                processingResult.Sequence,
                null,
                null,
                null,
                null,
                processingResult,
                leaseAlreadyDisposed ? null : lease);
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
                throw new InvalidOperationException("Ordered concurrent rebalance completion has no delta or result.");
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
                throw new InvalidOperationException("Ordered rebalance stale topology recompute requires the source batch.");
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

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            DisposeCurrentDelta();
            workerTelemetry = null;
            lease?.Dispose();
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
                    $"Queued rebalance migration failed with state {migrationResult.State}.",
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
