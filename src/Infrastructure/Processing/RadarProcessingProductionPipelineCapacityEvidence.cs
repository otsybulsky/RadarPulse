namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingProductionPipelineCapacityEvidence
{
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

    public string RunId { get; }

    public string ProfileName { get; }

    public TimeSpan Elapsed { get; }

    public long MeasuredAllocatedBytes { get; }

    public long AcceptedBatchCount { get; }

    public long ProcessedBatchCount { get; }

    public long CommittedBatchCount { get; }

    public RadarProcessingProductionPipelineHandlerMode HandlerMode { get; }

    public RadarProcessingProductionPipelineDurableAdapterKind DurableAdapterKind { get; }

    public long TerminalRetainedBatchCount { get; }

    public long TerminalRetainedPayloadBytes { get; }

    public bool ProcessingCompletenessPassed { get; }

    public bool IsReady { get; }

    public string FirstBlockingReason { get; }

    public string ConfigurationContour { get; }

    public bool HasBlockingReason => !string.IsNullOrWhiteSpace(FirstBlockingReason);

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
