using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingProductionPipelineControlResult
{
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

    public string RunId { get; }

    public RadarProcessingProductionPipelineFallbackAction Action { get; }

    public RadarProcessingProductionPipelineResolvedConfiguration Configuration { get; }

    public RadarProcessingProductionPipelineOperatorSummary OperatorSummary { get; }

    public RadarProcessingDurableAdapterSummary AdapterSummary { get; }

    public int CanceledOpenCount { get; }

    public int ReleasedCanceledCount { get; }

    public int DrainedProcessingCount { get; }

    public string Message { get; }

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
