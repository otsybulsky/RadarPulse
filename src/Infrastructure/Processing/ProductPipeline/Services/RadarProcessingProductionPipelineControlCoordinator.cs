using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Applies operator control actions to file-backed durable production-pipeline state.
/// </summary>
public sealed class RadarProcessingProductionPipelineControlCoordinator
{
    /// <summary>
    /// Reports a stopped posture while preserving durable state for later recovery.
    /// </summary>
    public RadarProcessingProductionPipelineControlResult StopAccepting(
        RadarProcessingProductionPipelineRecoveryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var configuration = RadarProcessingProductionPipelineProfile.Resolve(request.Options);
        var load = TryLoadQueue(request, out var queue, out var failed);
        if (!load)
        {
            return failed!;
        }

        var adapter = queue!.CreateAdapterSummary();
        var readiness = new RadarProcessingDurableRuntimeReadinessSummary(adapter.QueueSummary);
        var summary = RadarProcessingProductionPipelineOperatorSummary.Create(
            configuration,
            RadarProcessingProductionPipelineRunState.Stopped,
            readiness,
            adapter,
            processingComplete: !adapter.QueueSummary.HasBlockingEnvelope);
        return new RadarProcessingProductionPipelineControlResult(
            request.RunId,
            RadarProcessingProductionPipelineFallbackAction.StopAccepting,
            configuration,
            summary,
            adapter,
            message: "Pipeline is stopped for new work; durable state is preserved.");
    }

    /// <summary>
    /// Drains accepted durable work through a rebuilt processing session.
    /// </summary>
    public async ValueTask<RadarProcessingProductionPipelineControlResult> DrainAcceptedAsync(
        RadarProcessingProductionPipelineRecoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var configuration = RadarProcessingProductionPipelineProfile.Resolve(request.Options);
        var load = TryLoadQueue(request, out var queue, out var failed);
        if (!load)
        {
            return failed!;
        }

        var loadedQueue = queue!;
        await using var session = new RadarProcessingDurableProcessingSession(
            CreateCore(request, configuration),
            loadedQueue);
        var result = await session.DrainAsync("production-pipeline-drain", cancellationToken)
            .ConfigureAwait(false);
        var adapter = loadedQueue.CreateAdapterSummary();
        var readiness = new RadarProcessingDurableRuntimeReadinessSummary(result.QueueSummary);
        var summary = RadarProcessingProductionPipelineOperatorSummary.Create(
            configuration,
            RadarProcessingProductionPipelineRunState.Draining,
            readiness,
            adapter,
            processingComplete: result.IsCompleted);

        return new RadarProcessingProductionPipelineControlResult(
            request.RunId,
            RadarProcessingProductionPipelineFallbackAction.DrainAccepted,
            configuration,
            summary,
            adapter,
            drainedProcessingCount: result.ProcessingResults.Count,
            message: result.Message);
    }

    /// <summary>
    /// Cancels open durable envelopes and releases canceled resources.
    /// </summary>
    public RadarProcessingProductionPipelineControlResult CancelOpenAndRelease(
        RadarProcessingProductionPipelineRecoveryRequest request,
        string message = "Production pipeline cancel-open fallback.")
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(message);

        var configuration = RadarProcessingProductionPipelineProfile.Resolve(request.Options);
        var load = TryLoadQueue(request, out var queue, out var failed);
        if (!load)
        {
            return failed!;
        }

        var canceled = queue!.CancelOpen(message);
        var released = queue.ReleaseCanceled(message);
        var adapter = queue.CreateAdapterSummary();
        var readiness = new RadarProcessingDurableRuntimeReadinessSummary(adapter.QueueSummary);
        var summary = RadarProcessingProductionPipelineOperatorSummary.Create(
            configuration,
            RadarProcessingProductionPipelineRunState.Canceled,
            readiness,
            adapter,
            processingComplete: false);

        return new RadarProcessingProductionPipelineControlResult(
            request.RunId,
            RadarProcessingProductionPipelineFallbackAction.CancelOpenAndRelease,
            configuration,
            summary,
            adapter,
            canceled,
            released,
            message: message);
    }

    /// <summary>
    /// Produces an explicit rejection result for a fallback outside the accepted profile.
    /// </summary>
    public RadarProcessingProductionPipelineControlResult RejectUnsafeFallback(
        RadarProcessingProductionPipelineRecoveryRequest request,
        string message)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var options = request.Options ?? new RadarProcessingProductionPipelineOptions();
        var unsafeOptions = new RadarProcessingProductionPipelineOptions(
            options.ProfileName,
            options.ProviderMode,
            options.ProviderOverlapMode,
            options.RetentionStrategy,
            options.ExecutionMode,
            options.WorkerCount,
            options.WorkerQueueCapacity,
            options.ProviderQueueCapacity,
            options.RetainedPayloadBytes,
            options.OrderedActiveBatchCapacity,
            options.DurableAdapterKind,
            options.HandlerMode,
            options.WorkloadBatchLimit,
            silentBorrowedProviderFallback: true,
            options.OverrideSource);
        var configuration = RadarProcessingProductionPipelineProfile.Resolve(unsafeOptions);
        var adapter = new RadarProcessingDurableAdapterSummary(
            RadarProcessingFileDurableEnvelopeStore.Kind,
            RadarProcessingPersistentDurableEnvelopeRecord.CurrentSchemaVersion,
            Path.GetFullPath(request.DurableStorePath));
        var summary = RadarProcessingProductionPipelineOperatorSummary.Create(
            configuration,
            RadarProcessingProductionPipelineRunState.Blocked,
            processingComplete: false,
            warnings: new[] { message });

        return new RadarProcessingProductionPipelineControlResult(
            request.RunId,
            RadarProcessingProductionPipelineFallbackAction.RejectUnsafeFallback,
            configuration,
            summary,
            adapter,
            message: message);
    }

    private static bool TryLoadQueue(
        RadarProcessingProductionPipelineRecoveryRequest request,
        out RadarProcessingDurableEnvelopeQueue? queue,
        out RadarProcessingProductionPipelineControlResult? failed)
    {
        var configuration = RadarProcessingProductionPipelineProfile.Resolve(request.Options);
        if (!configuration.IsValid)
        {
            var adapter = new RadarProcessingDurableAdapterSummary(
                RadarProcessingFileDurableEnvelopeStore.Kind,
                RadarProcessingPersistentDurableEnvelopeRecord.CurrentSchemaVersion,
                Path.GetFullPath(request.DurableStorePath));
            var summary = RadarProcessingProductionPipelineOperatorSummary.Create(
                configuration,
                RadarProcessingProductionPipelineRunState.Blocked,
                processingComplete: false);
            queue = null;
            failed = new RadarProcessingProductionPipelineControlResult(
                request.RunId,
                RadarProcessingProductionPipelineFallbackAction.RejectUnsafeFallback,
                configuration,
                summary,
                adapter,
                message: summary.FirstBlockingReason);
            return false;
        }

        var store = new RadarProcessingFileDurableEnvelopeStore(request.DurableStorePath);
        try
        {
            queue = new RadarProcessingDurableEnvelopeQueue(store);
            failed = null;
            return true;
        }
        catch (InvalidOperationException exception)
        {
            var adapter = store.CreateSummary(
                compatibilityStatus: RadarProcessingDurableAdapterCompatibilityStatus.Failed,
                storageMessage: exception.Message);
            var summary = RadarProcessingProductionPipelineOperatorSummary.Create(
                configuration,
                RadarProcessingProductionPipelineRunState.Blocked,
                durableAdapter: adapter,
                processingComplete: false);
            queue = null;
            failed = new RadarProcessingProductionPipelineControlResult(
                request.RunId,
                RadarProcessingProductionPipelineFallbackAction.RejectUnsafeFallback,
                configuration,
                summary,
                adapter,
                message: exception.Message);
            return false;
        }
    }

    private static RadarProcessingCore CreateCore(
        RadarProcessingProductionPipelineRecoveryRequest request,
        RadarProcessingProductionPipelineResolvedConfiguration configuration) =>
        new(
            request.SourceUniverse,
            new RadarProcessingCoreOptions(
                configuration.ExecutionMode.Value,
                request.PartitionCount,
                request.ShardCount,
                enableValidation: true,
                request.Handlers,
                configuration.CreateAsyncExecution()));
}
