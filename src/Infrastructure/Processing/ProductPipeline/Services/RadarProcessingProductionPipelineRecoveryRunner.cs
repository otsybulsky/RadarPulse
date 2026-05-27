using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingProductionPipelineRecoveryRunner
{
    public async ValueTask<RadarProcessingProductionPipelineRecoveryResult> RecoverAsync(
        RadarProcessingProductionPipelineRecoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var configuration = RadarProcessingProductionPipelineProfile.Resolve(request.Options);
        if (!configuration.IsValid)
        {
            var blockedSummary = RadarProcessingProductionPipelineOperatorSummary.Create(
                configuration,
                RadarProcessingProductionPipelineRunState.Blocked,
                processingComplete: false);
            return new RadarProcessingProductionPipelineRecoveryResult(
                request.RunId,
                configuration,
                blockedSummary,
                CreateAdapterSummary(request, RadarProcessingDurableAdapterCompatibilityStatus.Empty),
                message: blockedSummary.FirstBlockingReason);
        }

        var store = new RadarProcessingFileDurableEnvelopeStore(request.DurableStorePath);
        RadarProcessingDurableEnvelopeQueue queue;
        try
        {
            queue = new RadarProcessingDurableEnvelopeQueue(store);
        }
        catch (InvalidOperationException exception)
        {
            var adapterSummary = store.CreateSummary(
                compatibilityStatus: RadarProcessingDurableAdapterCompatibilityStatus.Failed,
                storageMessage: exception.Message);
            var blockedSummary = RadarProcessingProductionPipelineOperatorSummary.Create(
                configuration,
                RadarProcessingProductionPipelineRunState.Blocked,
                durableAdapter: adapterSummary,
                processingComplete: false);
            return new RadarProcessingProductionPipelineRecoveryResult(
                request.RunId,
                configuration,
                blockedSummary,
                adapterSummary,
                message: exception.Message);
        }

        await using var session = new RadarProcessingDurableProcessingSession(
            CreateCore(request, configuration),
            queue);
        var recovered = await session.RecoverCompletedAsync(cancellationToken).ConfigureAwait(false);
        var committed = session.CommitReady(cancellationToken);
        var result = session.CreateResult();
        var adapter = queue.CreateAdapterSummary();
        var readiness = new RadarProcessingDurableRuntimeReadinessSummary(result.QueueSummary);
        var summary = RadarProcessingProductionPipelineOperatorSummary.Create(
            configuration,
            result.Status == RadarProcessingQueuedSessionStatus.Canceled
                ? RadarProcessingProductionPipelineRunState.Canceled
                : result.Status == RadarProcessingQueuedSessionStatus.Faulted
                    ? RadarProcessingProductionPipelineRunState.Failed
                    : RadarProcessingProductionPipelineRunState.Completed,
            readiness,
            adapter,
            processingComplete: !result.IsFaulted && !result.IsCanceled);

        return new RadarProcessingProductionPipelineRecoveryResult(
            request.RunId,
            configuration,
            summary,
            adapter,
            recovered,
            committed,
            result.Message);
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

    private static RadarProcessingDurableAdapterSummary CreateAdapterSummary(
        RadarProcessingProductionPipelineRecoveryRequest request,
        RadarProcessingDurableAdapterCompatibilityStatus compatibilityStatus) =>
        new(
            RadarProcessingFileDurableEnvelopeStore.Kind,
            RadarProcessingPersistentDurableEnvelopeRecord.CurrentSchemaVersion,
            Path.GetFullPath(request.DurableStorePath),
            compatibilityStatus);
}
