using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingQueuedRebalanceSession
{
    private static void DisposeCompleted(
        List<OrderedConcurrentRebalanceBatchCompletion> completed)
    {
        foreach (var completion in completed)
        {
            completion.Dispose();
        }

        completed.Clear();
    }

    private void RecordEnqueueResult(
        RadarProcessingQueuedBatchEnqueueResult result)
    {
        lock (sync)
        {
            enqueueResults.Add(result);
        }
    }

    private void RecordProcessingResult(
        RadarProcessingQueuedBatchProcessingResult result)
    {
        lock (sync)
        {
            processingResults.Add(result);
        }
    }

    private bool IsFaulted
    {
        get
        {
            lock (sync)
            {
                return faulted;
            }
        }
    }

    private void MarkFaulted(string message)
    {
        lock (sync)
        {
            faulted = true;
            faultMessage = message;
        }

        queue.Fault(message);
    }

    private void MarkCanceledAndRecordQueued()
    {
        var canceledQueued = MarkCanceled();
        foreach (var queuedBatch in canceledQueued)
        {
            RecordProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.Canceled(
                    queuedBatch.Sequence,
                    "Queued rebalance batch was canceled before dequeue."));
        }
    }

    private IReadOnlyList<RadarProcessingQueuedBatch> MarkCanceled()
    {
        lock (sync)
        {
            canceled = true;
        }

        return queue.Options.ShutdownMode == RadarProcessingProviderQueueShutdownMode.CancelQueued
            ? queue.CancelQueued()
            : CloseAndReturnNoCanceledBatches();
    }

    private IReadOnlyList<RadarProcessingQueuedBatch> CloseAndReturnNoCanceledBatches()
    {
        queue.Close();
        return Array.Empty<RadarProcessingQueuedBatch>();
    }

    private void AddDrainTime(long started)
    {
        lock (sync)
        {
            totalDrainTime += Stopwatch.GetElapsedTime(started);
        }
    }

    private RadarProcessingQueuedSessionStatus GetTerminalStatus()
    {
        lock (sync)
        {
            if (canceled)
            {
                return RadarProcessingQueuedSessionStatus.Canceled;
            }

            return faulted
                ? RadarProcessingQueuedSessionStatus.Faulted
                : RadarProcessingQueuedSessionStatus.Completed;
        }
    }

    private string GetTerminalMessage()
    {
        lock (sync)
        {
            return faulted ? faultMessage : string.Empty;
        }
    }

    private RadarProcessingQueuedSessionResult CreateSessionResult(
        RadarProcessingQueuedSessionStatus status,
        string message)
    {
        RadarProcessingQueuedBatchEnqueueResult[] enqueueSnapshot;
        RadarProcessingQueuedBatchProcessingResult[] processingSnapshot;
        TimeSpan drainTime;
        lock (sync)
        {
            enqueueSnapshot = enqueueResults.ToArray();
            processingSnapshot = processingResults.ToArray();
            drainTime = totalDrainTime;
        }

        return new RadarProcessingQueuedSessionResult(
            status,
            CreateTelemetrySummary(drainTime, processingSnapshot),
            enqueueSnapshot,
            processingSnapshot,
            message,
            rebalanceSession.CurrentTopology.Version);
    }

    private RadarProcessingProviderQueueTelemetrySummary CreateTelemetrySummary(
        TimeSpan drainTime,
        IReadOnlyCollection<RadarProcessingQueuedBatchProcessingResult> processingSnapshot)
    {
        var queueSummary = queue.CreateTelemetrySummary();
        var completed = processingSnapshot.LongCount(static result => result.Status == RadarProcessingQueuedBatchProcessingStatus.Succeeded);
        var failed = processingSnapshot.LongCount(static result =>
            result.Status == RadarProcessingQueuedBatchProcessingStatus.FailedProcessing ||
            result.Status == RadarProcessingQueuedBatchProcessingStatus.FailedValidation ||
            result.Status == RadarProcessingQueuedBatchProcessingStatus.FailedMigration);
        var canceledCount = processingSnapshot.LongCount(static result => result.Status == RadarProcessingQueuedBatchProcessingStatus.Canceled);
        var skipped = processingSnapshot.LongCount(static result => result.Status == RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault);

        return new RadarProcessingProviderQueueTelemetrySummary(
            queueSummary.OwnedSnapshotCount,
            queueSummary.OwnedSnapshotPayloadBytes,
            queueSummary.OwnedSnapshotAllocatedBytes,
            queueSummary.TotalOwnedSnapshotTime,
            queueSummary.EnqueueAttemptCount,
            queueSummary.EnqueuedBatchCount,
            queueSummary.EnqueueFullCount,
            queueSummary.EnqueueTimedOutCount,
            queueSummary.EnqueueCanceledCount,
            queueSummary.EnqueueClosedCount,
            queueSummary.EnqueueFaultedCount,
            queueSummary.TotalEnqueueWaitTime,
            queueSummary.DequeuedBatchCount,
            completed,
            failed,
            canceledCount,
            skipped,
            drainTime,
            queueSummary.QueueDepthHighWatermark,
            queueSummary.QueuedPayloadBytesHighWatermark,
            queueSummary.OwnedSnapshotPayloadValueCount,
            queueSummary.TotalProviderToProcessingLatency,
            queueSummary.RecentDetails,
            queueSummary.DroppedRecentDetailCount,
            queueSummary.OwnedSnapshotEventCount,
            queueSummary.TotalDequeueWaitTime,
            queueSummary.RetainedResourcePressure);
    }

    private static RadarProcessingAsyncRebalanceSession? CreateAsyncRebalanceSessionIfNeeded(
        RadarProcessingRebalanceSession rebalanceSession)
    {
        ArgumentNullException.ThrowIfNull(rebalanceSession);
        return rebalanceSession.Core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport
            ? new RadarProcessingAsyncRebalanceSession(rebalanceSession)
            : null;
    }

    private static bool RequiresAsyncRebalanceSession(
        RadarProcessingRebalanceSession rebalanceSession)
    {
        ArgumentNullException.ThrowIfNull(rebalanceSession);
        return rebalanceSession.Core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport;
    }
}
