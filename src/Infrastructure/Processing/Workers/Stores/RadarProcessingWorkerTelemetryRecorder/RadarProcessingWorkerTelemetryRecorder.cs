using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Aggregates async worker dispatch telemetry into bounded recent samples and counters.
/// </summary>
/// <remarks>
/// The recorder keeps always-on counters and conditionally retains recent batch
/// and failure details according to <see cref="RadarProcessingTelemetryRetentionOptions"/>.
/// It is used by infrastructure sessions to expose worker evidence without
/// changing processing outcomes.
/// </remarks>
public sealed partial class RadarProcessingWorkerTelemetryRecorder
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

    /// <summary>
    /// Creates a recorder with the selected diagnostic retention settings.
    /// </summary>
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

    /// <summary>
    /// Retention settings that control recent sample capture.
    /// </summary>
    public RadarProcessingTelemetryRetentionOptions Options => options;

    /// <summary>
    /// Records one dispatch result and updates counters, timings, and recent failures.
    /// </summary>
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

    /// <summary>
    /// Creates an immutable telemetry summary from the current counters and retained samples.
    /// </summary>
    public RadarProcessingWorkerTelemetrySummary CreateSummary() =>
        new(
            CreateCounters(),
            workerCount,
            queueCapacity,
            recentBatches.Snapshot(),
            recentFailures.Snapshot(),
            CreateRetentionStats());

    /// <summary>
    /// Clears counters and retained samples so the recorder can be reused for a new session.
    /// </summary>
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

}
