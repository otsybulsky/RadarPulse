using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingSyntheticRebalanceBenchmark
{
    private static async ValueTask<IterationTelemetry> RunOrderedRebalanceSessionIterationAsync(
        RadarProcessingSyntheticRebalanceWorkload workload,
        RadarProcessingRebalanceHardeningOptions hardeningOptions,
        RadarProcessingExecutionMode executionMode,
        RadarProcessingAsyncExecutionOptions? asyncExecution,
        RadarProcessingWorkerTelemetryRecorder? workerTelemetryRecorder,
        RadarProcessingAsyncWorkerGroup? workerGroup,
        int orderedActiveBatchCapacity,
        CancellationToken cancellationToken)
    {
        var session = workload.CreateSession(hardeningOptions, executionMode, asyncExecution);
        var initialTopologyVersion = session.CurrentTopology.Version;
        var telemetry = IterationTelemetry.Empty;
        var queueCapacity = Math.Max(orderedActiveBatchCapacity, checked((int)workload.BatchesPerIteration));
        var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(queueCapacity));
        RadarProcessingAsyncRebalanceSession? asyncSession = null;
        if (executionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            asyncSession = new RadarProcessingAsyncRebalanceSession(
                session,
                CreateAsyncCoreSession(session.Core, workerTelemetryRecorder, workerGroup),
                ownsAsyncCoreSession: true);
        }

        await using var queuedSession = new RadarProcessingQueuedRebalanceSession(
            session,
            queue,
            asyncSession,
            ownsQueue: true,
            ownsAsyncRebalanceSession: asyncSession is not null);

        foreach (var batch in workload.Batches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var enqueue = await queuedSession
                .EnqueueAsync(batch, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (!enqueue.IsAccepted)
            {
                throw new InvalidDataException($"Ordered rebalance synthetic enqueue failed with status {enqueue.Status}.");
            }
        }

        queuedSession.CompleteAdding();
        var result = await queuedSession
            .DrainOrderedConcurrentAsync(
                new RadarProcessingOrderedConcurrencyOptions(orderedActiveBatchCapacity),
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsCompleted)
        {
            throw new InvalidDataException(result.Message);
        }

        foreach (var processing in result.ProcessingResults)
        {
            if (!processing.IsSuccessful || processing.RebalanceResult is null)
            {
                throw new InvalidDataException(processing.Message);
            }

            telemetry = telemetry.Add(processing.RebalanceResult);
        }

        var metrics = session.Core.CreateMetrics();
        return telemetry.WithMetrics(
            metrics,
            session.CurrentTopology.Version.Value - initialTopologyVersion.Value + 1);
    }
}
