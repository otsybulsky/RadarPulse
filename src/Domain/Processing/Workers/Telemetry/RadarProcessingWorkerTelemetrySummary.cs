namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingWorkerTelemetrySummary
{
    private readonly IReadOnlyList<RadarProcessingRecentWorkerBatch> recentBatches;
    private readonly IReadOnlyList<RadarProcessingRecentWorkerFailure> recentFailures;

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

    public RadarProcessingWorkerTelemetryCounters Counters { get; }

    public int WorkerCount { get; }

    public int QueueCapacity { get; }

    public IReadOnlyList<RadarProcessingRecentWorkerBatch> RecentBatches => recentBatches;

    public IReadOnlyList<RadarProcessingRecentWorkerFailure> RecentFailures => recentFailures;

    public RadarProcessingWorkerRetentionStats RetentionStats { get; }

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
