namespace RadarPulse.Domain.Processing;

/// <summary>
/// Summarizes async worker group counters plus bounded recent batch and failure detail.
/// </summary>
public sealed class RadarProcessingWorkerTelemetrySummary
{
    private readonly IReadOnlyList<RadarProcessingRecentWorkerBatch> recentBatches;
    private readonly IReadOnlyList<RadarProcessingRecentWorkerFailure> recentFailures;

    /// <summary>
    /// Creates a worker telemetry summary and copies retained detail collections.
    /// </summary>
    public RadarProcessingWorkerTelemetrySummary(
        RadarProcessingWorkerTelemetryCounters counters,
        int workerCount,
        int queueCapacity,
        IReadOnlyCollection<RadarProcessingRecentWorkerBatch> recentBatches,
        IReadOnlyCollection<RadarProcessingRecentWorkerFailure> recentFailures,
        RadarProcessingWorkerRetentionStats retentionStats)
    {
        ArgumentNullException.ThrowIfNull(counters);
        ArgumentOutOfRangeException.ThrowIfNegative(workerCount);
        ArgumentOutOfRangeException.ThrowIfNegative(queueCapacity);
        ArgumentNullException.ThrowIfNull(recentBatches);
        ArgumentNullException.ThrowIfNull(recentFailures);
        ArgumentNullException.ThrowIfNull(retentionStats);

        Counters = counters;
        WorkerCount = workerCount;
        QueueCapacity = queueCapacity;
        this.recentBatches = CopyRequired(recentBatches, nameof(recentBatches));
        this.recentFailures = CopyRequired(recentFailures, nameof(recentFailures));
        RetentionStats = retentionStats;
    }

    /// <summary>
    /// Gets aggregate worker telemetry counters.
    /// </summary>
    public RadarProcessingWorkerTelemetryCounters Counters { get; }

    /// <summary>
    /// Gets the worker count associated with the telemetry snapshot.
    /// </summary>
    public int WorkerCount { get; }

    /// <summary>
    /// Gets the queue capacity associated with the telemetry snapshot.
    /// </summary>
    public int QueueCapacity { get; }

    /// <summary>
    /// Gets bounded recent batch telemetry in retention order.
    /// </summary>
    public IReadOnlyList<RadarProcessingRecentWorkerBatch> RecentBatches => recentBatches;

    /// <summary>
    /// Gets bounded recent failure telemetry in retention order.
    /// </summary>
    public IReadOnlyList<RadarProcessingRecentWorkerFailure> RecentFailures => recentFailures;

    /// <summary>
    /// Gets retention counters for retained and dropped detail.
    /// </summary>
    public RadarProcessingWorkerRetentionStats RetentionStats { get; }

    /// <summary>
    /// Gets an empty telemetry summary used when no worker group participated.
    /// </summary>
    public static RadarProcessingWorkerTelemetrySummary Empty { get; } =
        new(
            new RadarProcessingWorkerTelemetryCounters(),
            workerCount: 0,
            queueCapacity: 0,
            Array.Empty<RadarProcessingRecentWorkerBatch>(),
            Array.Empty<RadarProcessingRecentWorkerFailure>(),
            new RadarProcessingWorkerRetentionStats());

    private static IReadOnlyList<T> CopyRequired<T>(
        IReadOnlyCollection<T> values,
        string paramName)
        where T : class
    {
        if (values.Count == 0)
        {
            return Array.Empty<T>();
        }

        var result = new List<T>(values.Count);
        foreach (var value in values)
        {
            if (value is null)
            {
                throw new ArgumentNullException(paramName);
            }

            result.Add(value);
        }

        return Array.AsReadOnly(result.ToArray());
    }
}
