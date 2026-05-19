using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingWorkerTelemetryRecorder
{
    private readonly RadarProcessingTelemetryRetentionOptions options;
    private readonly RadarProcessingBoundedTelemetryWindow<RadarProcessingRecentWorkerBatch> recentBatches;
    private readonly RadarProcessingBoundedTelemetryWindow<RadarProcessingRecentWorkerFailure> recentFailures;

    private long dispatchedBatchCount;
    private long completedBatchCount;
    private long failedBatchCount;
    private long canceledBatchCount;
    private long timedOutBatchCount;
    private long rejectedDispatchCount;
    private long submittedWorkItemCount;
    private long acceptedWorkItemCount;
    private long completedWorkItemCount;
    private long succeededWorkItemCount;
    private long failedWorkItemCount;
    private long canceledWorkItemCount;
    private TimeSpan totalDispatchTime;
    private TimeSpan totalQueueWaitTime;
    private TimeSpan totalExecutionTime;
    private TimeSpan totalAggregationTime;
    private TimeSpan totalBarrierWaitTime;
    private int workerCount;
    private int queueCapacity;

    public RadarProcessingWorkerTelemetryRecorder(
        RadarProcessingTelemetryRetentionOptions? options = null)
    {
        this.options = options ?? RadarProcessingTelemetryRetentionOptions.Default;

        var retainDetail = this.options.RetentionMode is not RadarProcessingDiagnosticRetentionMode.Counters;
        recentBatches = new RadarProcessingBoundedTelemetryWindow<RadarProcessingRecentWorkerBatch>(
            retainDetail ? this.options.MaxRetainedWorkerBatches : 0);
        recentFailures = new RadarProcessingBoundedTelemetryWindow<RadarProcessingRecentWorkerFailure>(
            retainDetail ? this.options.MaxRetainedWorkerFailures : 0);
    }

    public RadarProcessingTelemetryRetentionOptions Options => options;

    public void RecordDispatch(
        RadarProcessingAsyncDispatchResult dispatchResult,
        TimeSpan dispatchTime = default,
        TimeSpan aggregationTime = default)
    {
        ArgumentNullException.ThrowIfNull(dispatchResult);
        ThrowIfNegative(dispatchTime, nameof(dispatchTime));
        ThrowIfNegative(aggregationTime, nameof(aggregationTime));

        var batch = CreateRecentBatch(dispatchResult, dispatchTime, aggregationTime);
        RecordCounters(batch);
        AddRecentBatch(batch);
        RecordFailureSamples(dispatchResult);
    }

    public RadarProcessingWorkerTelemetrySummary CreateSummary() =>
        new(
            CreateCounters(),
            workerCount,
            queueCapacity,
            recentBatches.Snapshot(),
            recentFailures.Snapshot(),
            CreateRetentionStats());

    public void Reset()
    {
        dispatchedBatchCount = 0;
        completedBatchCount = 0;
        failedBatchCount = 0;
        canceledBatchCount = 0;
        timedOutBatchCount = 0;
        rejectedDispatchCount = 0;
        submittedWorkItemCount = 0;
        acceptedWorkItemCount = 0;
        completedWorkItemCount = 0;
        succeededWorkItemCount = 0;
        failedWorkItemCount = 0;
        canceledWorkItemCount = 0;
        totalDispatchTime = TimeSpan.Zero;
        totalQueueWaitTime = TimeSpan.Zero;
        totalExecutionTime = TimeSpan.Zero;
        totalAggregationTime = TimeSpan.Zero;
        totalBarrierWaitTime = TimeSpan.Zero;
        workerCount = 0;
        queueCapacity = 0;
        recentBatches.Clear();
        recentFailures.Clear();
    }

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
