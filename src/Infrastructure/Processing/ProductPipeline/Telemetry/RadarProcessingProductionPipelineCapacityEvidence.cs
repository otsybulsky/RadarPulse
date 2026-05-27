namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Capacity, allocation, and readiness evidence extracted from a production-pipeline run.
/// </summary>
public sealed class RadarProcessingProductionPipelineCapacityEvidence
{
    /// <summary>
    /// Creates capacity evidence for product and operator read models.
    /// </summary>
    public RadarProcessingProductionPipelineCapacityEvidence(
        string runId,
        string profileName,
        TimeSpan elapsed,
        long measuredAllocatedBytes,
        long acceptedBatchCount,
        long processedBatchCount,
        long committedBatchCount,
        RadarProcessingProductionPipelineHandlerMode handlerMode,
        RadarProcessingProductionPipelineDurableAdapterKind durableAdapterKind,
        long terminalRetainedBatchCount,
        long terminalRetainedPayloadBytes,
        bool processingCompletenessPassed,
        bool isReady,
        string firstBlockingReason,
        string configurationContour)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(measuredAllocatedBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(acceptedBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(processedBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(committedBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(terminalRetainedBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(terminalRetainedPayloadBytes);
        ArgumentNullException.ThrowIfNull(firstBlockingReason);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationContour);

        RunId = runId;
        ProfileName = profileName;
        Elapsed = elapsed;
        MeasuredAllocatedBytes = measuredAllocatedBytes;
        AcceptedBatchCount = acceptedBatchCount;
        ProcessedBatchCount = processedBatchCount;
        CommittedBatchCount = committedBatchCount;
        HandlerMode = handlerMode;
        DurableAdapterKind = durableAdapterKind;
        TerminalRetainedBatchCount = terminalRetainedBatchCount;
        TerminalRetainedPayloadBytes = terminalRetainedPayloadBytes;
        ProcessingCompletenessPassed = processingCompletenessPassed;
        IsReady = isReady;
        FirstBlockingReason = firstBlockingReason;
        ConfigurationContour = configurationContour;
    }

    /// <summary>
    /// Stable run id.
    /// </summary>
    public string RunId { get; }

    /// <summary>
    /// Profile name used for the run.
    /// </summary>
    public string ProfileName { get; }

    /// <summary>
    /// Runtime elapsed time.
    /// </summary>
    public TimeSpan Elapsed { get; }

    /// <summary>
    /// Allocation evidence measured by the runtime.
    /// </summary>
    public long MeasuredAllocatedBytes { get; }

    /// <summary>
    /// Number of accepted provider batches.
    /// </summary>
    public long AcceptedBatchCount { get; }

    /// <summary>
    /// Number of processing results recorded.
    /// </summary>
    public long ProcessedBatchCount { get; }

    /// <summary>
    /// Number of successful committed processing results.
    /// </summary>
    public long CommittedBatchCount { get; }

    /// <summary>
    /// Handler output mode used by the run.
    /// </summary>
    public RadarProcessingProductionPipelineHandlerMode HandlerMode { get; }

    /// <summary>
    /// Durable adapter kind used by the run.
    /// </summary>
    public RadarProcessingProductionPipelineDurableAdapterKind DurableAdapterKind { get; }

    /// <summary>
    /// Terminal retained batch count.
    /// </summary>
    public long TerminalRetainedBatchCount { get; }

    /// <summary>
    /// Terminal retained payload bytes.
    /// </summary>
    public long TerminalRetainedPayloadBytes { get; }

    /// <summary>
    /// Indicates whether processing completeness evidence passed.
    /// </summary>
    public bool ProcessingCompletenessPassed { get; }

    /// <summary>
    /// Indicates whether the run was ready after evidence collection.
    /// </summary>
    public bool IsReady { get; }

    /// <summary>
    /// First blocking reason when the run is not ready.
    /// </summary>
    public string FirstBlockingReason { get; }

    /// <summary>
    /// Compact configuration contour for operator display.
    /// </summary>
    public string ConfigurationContour { get; }

    /// <summary>
    /// Indicates whether a blocking reason is present.
    /// </summary>
    public bool HasBlockingReason => !string.IsNullOrWhiteSpace(FirstBlockingReason);

    /// <summary>
    /// Creates capacity evidence from a production-pipeline run result.
    /// </summary>
    public static RadarProcessingProductionPipelineCapacityEvidence FromRunResult(
        RadarProcessingProductionPipelineRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var runtime = result.RuntimeResult;
        var session = runtime?.OverlapResult.Consumer.SessionResult;
        var accepted = session?.EnqueueResults.LongCount(static item => item.IsAccepted) ?? 0;
        if (accepted == 0)
        {
            accepted = runtime?.OverlapResult.QueueTelemetry.EnqueuedBatchCount ?? 0;
        }
        var processed = session?.ProcessingResults.Count ?? 0;
        var committed = session?.ProcessingResults.LongCount(static item => item.IsSuccessful) ?? 0;
        var elapsed = runtime?.OverlapResult.Elapsed ?? TimeSpan.Zero;
        var allocated = runtime?.OverlapResult.OverlapTelemetry.MeasuredAllocatedBytes ?? 0;
        var processingComplete = result.ReadModel?.Diagnostics.ProcessingCompletenessPassed ??
                                 result.OperatorSummary.ProcessingComplete;

        return new RadarProcessingProductionPipelineCapacityEvidence(
            result.RunId,
            result.Configuration.ProfileName,
            elapsed,
            allocated,
            accepted,
            processed,
            committed,
            result.OperatorSummary.HandlerMode,
            result.Configuration.DurableAdapterKind.Value,
            result.OperatorSummary.CurrentRetainedBatchCount,
            result.OperatorSummary.CurrentRetainedPayloadBytes,
            processingComplete,
            result.OperatorSummary.IsReady,
            result.OperatorSummary.FirstBlockingReason,
            CreateConfigurationContour(result.Configuration));
    }

    private static string CreateConfigurationContour(
        RadarProcessingProductionPipelineResolvedConfiguration configuration) =>
        string.Join(
            ", ",
            $"provider={configuration.ProviderMode.Value}",
            $"overlap={configuration.ProviderOverlapMode.Value}",
            $"retention={configuration.RetentionStrategy.Value}",
            $"execution={configuration.ExecutionMode.Value}",
            $"workers={configuration.WorkerCount.Value}",
            $"workerQueue={configuration.WorkerQueueCapacity.Value}",
            $"providerQueue={configuration.ProviderQueueCapacity.Value}",
            $"retainedBytes={configuration.RetainedPayloadBytes.Value}",
            $"activeBatches={configuration.OrderedActiveBatchCapacity.Value}",
            $"durable={configuration.DurableAdapterKind.Value}");
}
