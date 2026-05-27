using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingProductionPipelineRecoveryResult
{
    private readonly IReadOnlyList<RadarProcessingQueuedBatchProcessingResult> committedResults;

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

    public string RunId { get; }

    public RadarProcessingProductionPipelineResolvedConfiguration Configuration { get; }

    public RadarProcessingProductionPipelineOperatorSummary OperatorSummary { get; }

    public RadarProcessingDurableAdapterSummary AdapterSummary { get; }

    public int RecoveredCompletedCount { get; }

    public IReadOnlyList<RadarProcessingQueuedBatchProcessingResult> CommittedResults => committedResults;

    public string Message { get; }

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
