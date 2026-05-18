namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingRebalanceTelemetrySummary
{
    private readonly IReadOnlyList<RadarProcessingRebalanceSkippedReasonCounter> skippedReasonCounters;
    private readonly IReadOnlyList<RadarProcessingRebalanceRecentDecision> recentDecisions;
    private readonly IReadOnlyList<RadarProcessingRebalanceRecentLifecycleTransition> recentLifecycleTransitions;
    private readonly IReadOnlyList<RadarProcessingRebalanceRecentAcceptedMove> recentAcceptedMoves;
    private readonly IReadOnlyList<RadarProcessingRebalanceRecentValidationFailure> recentValidationFailures;

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

    public RadarProcessingRebalanceTelemetryCounters Counters { get; }

    public IReadOnlyList<RadarProcessingRebalanceSkippedReasonCounter> SkippedReasonCounters =>
        skippedReasonCounters;

    public IReadOnlyList<RadarProcessingRebalanceRecentDecision> RecentDecisions =>
        recentDecisions;

    public IReadOnlyList<RadarProcessingRebalanceRecentLifecycleTransition> RecentLifecycleTransitions =>
        recentLifecycleTransitions;

    public IReadOnlyList<RadarProcessingRebalanceRecentAcceptedMove> RecentAcceptedMoves =>
        recentAcceptedMoves;

    public IReadOnlyList<RadarProcessingRebalanceRecentValidationFailure> RecentValidationFailures =>
        recentValidationFailures;

    public RadarProcessingRebalanceRetentionStats RetentionStats { get; }

    public static RadarProcessingRebalanceTelemetrySummary Empty { get; } =
        new(
            new RadarProcessingRebalanceTelemetryCounters(),
            Array.Empty<RadarProcessingRebalanceSkippedReasonCounter>(),
            Array.Empty<RadarProcessingRebalanceRecentDecision>(),
            Array.Empty<RadarProcessingRebalanceRecentAcceptedMove>(),
            Array.Empty<RadarProcessingRebalanceRecentValidationFailure>(),
            new RadarProcessingRebalanceRetentionStats());

    private static IReadOnlyList<RadarProcessingRebalanceSkippedReasonCounter> CopySkippedReasonCounters(
        IEnumerable<RadarProcessingRebalanceSkippedReasonCounter> counters)
    {
        var result = new List<RadarProcessingRebalanceSkippedReasonCounter>();
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
        IEnumerable<T> values,
        string paramName)
        where T : class
    {
        var result = new List<T>();

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
