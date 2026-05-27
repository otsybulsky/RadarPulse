namespace RadarPulse.Domain.Processing;

/// <summary>
/// Snapshot of rebalance counters and retained diagnostic detail.
/// </summary>
public sealed class RadarProcessingRebalanceTelemetrySummary
{
    private readonly IReadOnlyList<RadarProcessingRebalanceSkippedReasonCounter> skippedReasonCounters;
    private readonly IReadOnlyList<RadarProcessingRebalanceRecentDecision> recentDecisions;
    private readonly IReadOnlyList<RadarProcessingRebalanceRecentLifecycleTransition> recentLifecycleTransitions;
    private readonly IReadOnlyList<RadarProcessingRebalanceRecentAcceptedMove> recentAcceptedMoves;
    private readonly IReadOnlyList<RadarProcessingRebalanceRecentValidationFailure> recentValidationFailures;

    /// <summary>
    /// Creates a rebalance telemetry summary.
    /// </summary>
    public RadarProcessingRebalanceTelemetrySummary(
        RadarProcessingRebalanceTelemetryCounters counters,
        IReadOnlyCollection<RadarProcessingRebalanceSkippedReasonCounter> skippedReasonCounters,
        IReadOnlyCollection<RadarProcessingRebalanceRecentDecision> recentDecisions,
        IReadOnlyCollection<RadarProcessingRebalanceRecentAcceptedMove> recentAcceptedMoves,
        IReadOnlyCollection<RadarProcessingRebalanceRecentValidationFailure> recentValidationFailures,
        RadarProcessingRebalanceRetentionStats retentionStats,
        IReadOnlyCollection<RadarProcessingRebalanceRecentLifecycleTransition>? recentLifecycleTransitions = null)
    {
        ArgumentNullException.ThrowIfNull(counters);
        ArgumentNullException.ThrowIfNull(skippedReasonCounters);
        ArgumentNullException.ThrowIfNull(recentDecisions);
        ArgumentNullException.ThrowIfNull(recentAcceptedMoves);
        ArgumentNullException.ThrowIfNull(recentValidationFailures);
        ArgumentNullException.ThrowIfNull(retentionStats);

        Counters = counters;
        this.skippedReasonCounters = CopySkippedReasonCounters(skippedReasonCounters);
        this.recentDecisions = CopyRequired(recentDecisions, nameof(recentDecisions));
        this.recentLifecycleTransitions = CopyRequired(
            recentLifecycleTransitions ?? Array.Empty<RadarProcessingRebalanceRecentLifecycleTransition>(),
            nameof(recentLifecycleTransitions));
        this.recentAcceptedMoves = CopyRequired(recentAcceptedMoves, nameof(recentAcceptedMoves));
        this.recentValidationFailures = CopyRequired(recentValidationFailures, nameof(recentValidationFailures));
        RetentionStats = retentionStats;
    }

    /// <summary>
    /// Aggregate rebalance counters.
    /// </summary>
    public RadarProcessingRebalanceTelemetryCounters Counters { get; }

    /// <summary>
    /// Skipped-reason counters sorted by recorder output.
    /// </summary>
    public IReadOnlyList<RadarProcessingRebalanceSkippedReasonCounter> SkippedReasonCounters =>
        skippedReasonCounters;

    /// <summary>
    /// Bounded recent decision detail.
    /// </summary>
    public IReadOnlyList<RadarProcessingRebalanceRecentDecision> RecentDecisions =>
        recentDecisions;

    /// <summary>
    /// Bounded recent quarantine lifecycle transition detail.
    /// </summary>
    public IReadOnlyList<RadarProcessingRebalanceRecentLifecycleTransition> RecentLifecycleTransitions =>
        recentLifecycleTransitions;

    /// <summary>
    /// Bounded recent accepted move detail.
    /// </summary>
    public IReadOnlyList<RadarProcessingRebalanceRecentAcceptedMove> RecentAcceptedMoves =>
        recentAcceptedMoves;

    /// <summary>
    /// Bounded recent validation failure detail.
    /// </summary>
    public IReadOnlyList<RadarProcessingRebalanceRecentValidationFailure> RecentValidationFailures =>
        recentValidationFailures;

    /// <summary>
    /// Retention and drop counts for detail windows.
    /// </summary>
    public RadarProcessingRebalanceRetentionStats RetentionStats { get; }

    /// <summary>
    /// Empty telemetry summary.
    /// </summary>
    public static RadarProcessingRebalanceTelemetrySummary Empty { get; } =
        new(
            new RadarProcessingRebalanceTelemetryCounters(),
            Array.Empty<RadarProcessingRebalanceSkippedReasonCounter>(),
            Array.Empty<RadarProcessingRebalanceRecentDecision>(),
            Array.Empty<RadarProcessingRebalanceRecentAcceptedMove>(),
            Array.Empty<RadarProcessingRebalanceRecentValidationFailure>(),
            new RadarProcessingRebalanceRetentionStats());

    private static IReadOnlyList<RadarProcessingRebalanceSkippedReasonCounter> CopySkippedReasonCounters(
        IReadOnlyCollection<RadarProcessingRebalanceSkippedReasonCounter> counters)
    {
        if (counters.Count == 0)
        {
            return Array.Empty<RadarProcessingRebalanceSkippedReasonCounter>();
        }

        var result = new List<RadarProcessingRebalanceSkippedReasonCounter>(counters.Count);
        var seen = new HashSet<RadarProcessingRebalanceSkippedReason>();

        foreach (var counter in counters)
        {
            ArgumentNullException.ThrowIfNull(counter);

            if (!seen.Add(counter.Reason))
            {
                throw new ArgumentException("Skipped reason counters must not contain duplicate reasons.", nameof(counters));
            }

            result.Add(counter);
        }

        return Array.AsReadOnly(result.ToArray());
    }

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
