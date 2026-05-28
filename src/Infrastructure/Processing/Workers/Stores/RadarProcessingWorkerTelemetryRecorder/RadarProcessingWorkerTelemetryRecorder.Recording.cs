using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingWorkerTelemetryRecorder
{
    private static RadarProcessingRecentWorkerBatch CreateRecentBatch(
        RadarProcessingAsyncDispatchResult dispatchResult,
        TimeSpan dispatchTime,
        TimeSpan aggregationTime)
    {
        var workerResult = dispatchResult.WorkerGroupResult;
        var completion = workerResult.BatchResult?.Completion;
        return new RadarProcessingRecentWorkerBatch(
            dispatchResult.Plan.BatchSequence,
            dispatchResult.TopologyVersion,
            workerResult.Status.WorkerCount,
            workerResult.Status.QueueCapacity,
            dispatchResult.Plan.ExpectedWorkItemCount,
            workerResult.DrainResult.AcceptedWorkItemCount,
            completion?.RecordedWorkItemCount ?? workerResult.DrainResult.CompletedWorkItemCount,
            completion?.SucceededWorkItemCount ?? 0,
            completion?.FailedWorkItemCount ?? 0,
            completion?.CanceledWorkItemCount ?? 0,
            workerResult.IsSuccess,
            workerResult.IsRejected,
            workerResult.TimeoutResult.TimedOut || workerResult.DrainResult.TimedOut,
            workerResult.FailureKind,
            workerResult.CancellationKind,
            dispatchTime,
            completion?.TotalQueueWaitTime ?? TimeSpan.Zero,
            completion?.TotalExecutionTime ?? TimeSpan.Zero,
            aggregationTime,
            workerResult.DrainResult.BarrierWaitTime);
    }

    private void RecordCounters(
        RadarProcessingRecentWorkerBatch batch)
    {
        dispatchedBatchCount++;
        if (batch.IsSuccessful)
        {
            completedBatchCount++;
        }

        if (batch.FailureKind != RadarProcessingAsyncFailureKind.None)
        {
            failedBatchCount++;
        }

        if (batch.CancellationKind != RadarProcessingAsyncCancellationKind.None)
        {
            canceledBatchCount++;
        }

        if (batch.TimedOut)
        {
            timedOutBatchCount++;
        }

        if (batch.IsRejected)
        {
            rejectedDispatchCount++;
        }

        submittedWorkItemCount += batch.SubmittedWorkItemCount;
        acceptedWorkItemCount += batch.AcceptedWorkItemCount;
        completedWorkItemCount += batch.CompletedWorkItemCount;
        succeededWorkItemCount += batch.SucceededWorkItemCount;
        failedWorkItemCount += batch.FailedWorkItemCount;
        canceledWorkItemCount += batch.CanceledWorkItemCount;
        totalDispatchTime += batch.DispatchTime;
        totalQueueWaitTime += batch.QueueWaitTime;
        totalExecutionTime += batch.ExecutionTime;
        totalAggregationTime += batch.AggregationTime;
        totalBarrierWaitTime += batch.BarrierWaitTime;
        workerCount = batch.WorkerCount;
        queueCapacity = batch.QueueCapacity;
    }

    private void RecordFailureSamples(
        RadarProcessingAsyncDispatchResult dispatchResult)
    {
        var workerResult = dispatchResult.WorkerGroupResult;
        var recordedWorkItemSample = false;
        if (workerResult.BatchResult is not null)
        {
            foreach (var completion in workerResult.BatchResult.Completion.Completions)
            {
                if (!completion.IsFailed && !completion.IsCanceled)
                {
                    continue;
                }

                var workItem = dispatchResult.Plan.WorkItems[completion.WorkItemId];
                AddRecentFailure(
                    new RadarProcessingRecentWorkerFailure(
                        completion.BatchSequence,
                        completion.TopologyVersion,
                        completion.FailureKind,
                        completion.CancellationKind,
                        completion.WorkItemId,
                        completion.WorkerId,
                        workItem.ShardId,
                        workerResult.TimeoutResult.TimedOut));
                recordedWorkItemSample = true;
            }
        }

        if (workerResult.FailureKind == RadarProcessingAsyncFailureKind.None &&
            workerResult.CancellationKind == RadarProcessingAsyncCancellationKind.None &&
            !workerResult.TimeoutResult.TimedOut)
        {
            return;
        }

        if (recordedWorkItemSample &&
            !workerResult.IsRejected &&
            !workerResult.TimeoutResult.TimedOut)
        {
            return;
        }

        AddRecentFailure(
            new RadarProcessingRecentWorkerFailure(
                dispatchResult.Plan.BatchSequence,
                dispatchResult.TopologyVersion,
                workerResult.FailureKind,
                workerResult.CancellationKind,
                timedOut: workerResult.TimeoutResult.TimedOut));
    }

    private void AddRecentBatch(
        RadarProcessingRecentWorkerBatch batch)
    {
        if (!recentBatches.CanRetain)
        {
            recentBatches.Drop();
            return;
        }

        recentBatches.Add(batch);
    }

    private void AddRecentFailure(
        RadarProcessingRecentWorkerFailure failure)
    {
        if (!recentFailures.CanRetain)
        {
            recentFailures.Drop();
            return;
        }

        recentFailures.Add(failure);
    }

    private RadarProcessingWorkerTelemetryCounters CreateCounters() =>
        new(
            dispatchedBatchCount,
            completedBatchCount,
            failedBatchCount,
            canceledBatchCount,
            timedOutBatchCount,
            rejectedDispatchCount,
            submittedWorkItemCount,
            acceptedWorkItemCount,
            completedWorkItemCount,
            succeededWorkItemCount,
            failedWorkItemCount,
            canceledWorkItemCount,
            totalDispatchTime,
            totalQueueWaitTime,
            totalExecutionTime,
            totalAggregationTime,
            totalBarrierWaitTime);

    private RadarProcessingWorkerRetentionStats CreateRetentionStats() =>
        new(
            recentBatches.Count,
            recentBatches.DroppedCount,
            recentFailures.Count,
            recentFailures.DroppedCount);

    private static void ThrowIfNegative(
        TimeSpan value,
        string paramName)
    {
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Duration must be non-negative.");
        }
    }
}
