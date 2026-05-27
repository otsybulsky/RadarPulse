using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Result of recovering completed durable work for a production-pipeline run.
/// </summary>
public sealed class RadarProcessingProductionPipelineRecoveryResult
{
    private readonly IReadOnlyList<RadarProcessingQueuedBatchProcessingResult> committedResults;

    /// <summary>
    /// Creates a recovery result with committed processing evidence.
    /// </summary>
    public RadarProcessingProductionPipelineRecoveryResult(
        string runId,
        RadarProcessingProductionPipelineResolvedConfiguration configuration,
        RadarProcessingProductionPipelineOperatorSummary operatorSummary,
        RadarProcessingDurableAdapterSummary adapterSummary,
        int recoveredCompletedCount = 0,
        IReadOnlyList<RadarProcessingQueuedBatchProcessingResult>? committedResults = null,
        string message = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(operatorSummary);
        ArgumentNullException.ThrowIfNull(adapterSummary);
        ArgumentOutOfRangeException.ThrowIfNegative(recoveredCompletedCount);
        ArgumentNullException.ThrowIfNull(message);

        RunId = runId;
        Configuration = configuration;
        OperatorSummary = operatorSummary;
        AdapterSummary = adapterSummary;
        RecoveredCompletedCount = recoveredCompletedCount;
        this.committedResults = CopyResults(committedResults);
        Message = message;
    }

    /// <summary>
    /// Stable run id.
    /// </summary>
    public string RunId { get; }

    /// <summary>
    /// Resolved configuration used for recovery.
    /// </summary>
    public RadarProcessingProductionPipelineResolvedConfiguration Configuration { get; }

    /// <summary>
    /// Operator readiness summary after recovery.
    /// </summary>
    public RadarProcessingProductionPipelineOperatorSummary OperatorSummary { get; }

    /// <summary>
    /// Durable adapter evidence after recovery.
    /// </summary>
    public RadarProcessingDurableAdapterSummary AdapterSummary { get; }

    /// <summary>
    /// Number of completed envelopes staged for commit from persisted state.
    /// </summary>
    public int RecoveredCompletedCount { get; }

    /// <summary>
    /// Processing results committed during recovery.
    /// </summary>
    public IReadOnlyList<RadarProcessingQueuedBatchProcessingResult> CommittedResults => committedResults;

    /// <summary>
    /// Recovery outcome message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Indicates whether the resulting operator summary is ready.
    /// </summary>
    public bool IsReady => OperatorSummary.IsReady;

    private static IReadOnlyList<RadarProcessingQueuedBatchProcessingResult> CopyResults(
        IReadOnlyList<RadarProcessingQueuedBatchProcessingResult>? values)
    {
        if (values is null || values.Count == 0)
        {
            return Array.Empty<RadarProcessingQueuedBatchProcessingResult>();
        }

        var copy = new RadarProcessingQueuedBatchProcessingResult[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            copy[i] = values[i] ?? throw new ArgumentNullException(nameof(values));
        }

        return Array.AsReadOnly(copy);
    }
}
