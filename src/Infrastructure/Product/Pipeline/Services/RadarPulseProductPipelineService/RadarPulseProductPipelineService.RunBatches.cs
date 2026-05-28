using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Infrastructure.Product;

public sealed partial class RadarPulseProductPipelineService : IRadarPulseProductPipelineService
{
    private async ValueTask<RadarPulseProductRunDetail> RunBatchesAsync(
        string runId,
        RadarSourceUniverse sourceUniverse,
        IReadOnlyCollection<RadarEventBatch> batches,
        int partitionCount,
        int shardCount,
        RadarPulseProductHandlerSet handlerSet,
        RadarPulseProductPipelineOptions? options,
        RadarPulseProductInputSummary input,
        CancellationToken cancellationToken)
    {
        var productionRequest = new RadarProcessingProductionPipelineRunRequest(
            runId,
            sourceUniverse,
            batches,
            partitionCount,
            shardCount,
            RadarPulseProductHandlerFactory.Create(handlerSet),
            CreateProductionOptions(options));
        var productionResult = await runner.RunAsync(productionRequest, cancellationToken)
            .ConfigureAwait(false);
        var detail = RadarPulseProductPipelineMapper.ToProductRunDetail(
            productionResult,
            input);
        historyStore.Store(detail);
        return detail;
    }

    private static RadarProcessingProductionPipelineOptions? CreateProductionOptions(
        RadarPulseProductPipelineOptions? options)
    {
        if (options is null)
        {
            return null;
        }

        return new RadarProcessingProductionPipelineOptions(
            workerCount: options.WorkerCount,
            workerQueueCapacity: options.WorkerQueueCapacity,
            providerQueueCapacity: options.ProviderQueueCapacity,
            retainedPayloadBytes: options.RetainedPayloadBytes,
            orderedActiveBatchCapacity: options.OrderedActiveBatchCapacity,
            workloadBatchLimit: options.WorkloadBatchLimit,
            silentBorrowedProviderFallback: options.SilentBorrowedProviderFallback);
    }

    private static void ValidateRunId(
        string runId) =>
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

    private static int ResolvePartitionCount(
        int requested,
        int sourceCount)
    {
        if (requested == 0)
        {
            return Math.Max(sourceCount, 1);
        }

        ArgumentOutOfRangeException.ThrowIfNegative(requested);
        return requested;
    }

    private static int ResolveShardCount(
        int requested,
        int partitionCount)
    {
        if (requested == 0)
        {
            return Math.Max(1, Math.Min(partitionCount, 4));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(requested);
        if (requested > partitionCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requested),
                requested,
                "Shard count must be less than or equal to partition count.");
        }

        return requested;
    }
}
