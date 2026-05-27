using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Result of applying a production-pipeline control action.
/// </summary>
public sealed class RadarProcessingProductionPipelineControlResult
{
    /// <summary>
    /// Creates a control result with durable adapter and readiness evidence.
    /// </summary>
    public RadarProcessingProductionPipelineControlResult(
        string runId,
        RadarProcessingProductionPipelineFallbackAction action,
        RadarProcessingProductionPipelineResolvedConfiguration configuration,
        RadarProcessingProductionPipelineOperatorSummary operatorSummary,
        RadarProcessingDurableAdapterSummary adapterSummary,
        int canceledOpenCount = 0,
        int releasedCanceledCount = 0,
        int drainedProcessingCount = 0,
        string message = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        EnsureKnownAction(action);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(operatorSummary);
        ArgumentNullException.ThrowIfNull(adapterSummary);
        ArgumentOutOfRangeException.ThrowIfNegative(canceledOpenCount);
        ArgumentOutOfRangeException.ThrowIfNegative(releasedCanceledCount);
        ArgumentOutOfRangeException.ThrowIfNegative(drainedProcessingCount);
        ArgumentNullException.ThrowIfNull(message);

        RunId = runId;
        Action = action;
        Configuration = configuration;
        OperatorSummary = operatorSummary;
        AdapterSummary = adapterSummary;
        CanceledOpenCount = canceledOpenCount;
        ReleasedCanceledCount = releasedCanceledCount;
        DrainedProcessingCount = drainedProcessingCount;
        Message = message;
    }

    /// <summary>
    /// Stable run id.
    /// </summary>
    public string RunId { get; }

    /// <summary>
    /// Control action applied.
    /// </summary>
    public RadarProcessingProductionPipelineFallbackAction Action { get; }

    /// <summary>
    /// Resolved configuration used for control.
    /// </summary>
    public RadarProcessingProductionPipelineResolvedConfiguration Configuration { get; }

    /// <summary>
    /// Operator readiness summary after control.
    /// </summary>
    public RadarProcessingProductionPipelineOperatorSummary OperatorSummary { get; }

    /// <summary>
    /// Durable adapter evidence after control.
    /// </summary>
    public RadarProcessingDurableAdapterSummary AdapterSummary { get; }

    /// <summary>
    /// Number of open envelopes canceled by the action.
    /// </summary>
    public int CanceledOpenCount { get; }

    /// <summary>
    /// Number of canceled envelopes released by the action.
    /// </summary>
    public int ReleasedCanceledCount { get; }

    /// <summary>
    /// Number of durable processing results drained by the action.
    /// </summary>
    public int DrainedProcessingCount { get; }

    /// <summary>
    /// Control outcome message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Indicates whether the resulting operator summary is ready.
    /// </summary>
    public bool IsReady => OperatorSummary.IsReady;

    internal static void EnsureKnownAction(
        RadarProcessingProductionPipelineFallbackAction action)
    {
        if (action is not RadarProcessingProductionPipelineFallbackAction.None and
            not RadarProcessingProductionPipelineFallbackAction.StopAccepting and
            not RadarProcessingProductionPipelineFallbackAction.DrainAccepted and
            not RadarProcessingProductionPipelineFallbackAction.CancelOpenAndRelease and
            not RadarProcessingProductionPipelineFallbackAction.RejectUnsafeFallback)
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }
    }
}
