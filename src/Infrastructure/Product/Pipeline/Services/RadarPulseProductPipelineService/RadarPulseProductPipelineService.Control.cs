using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Infrastructure.Product;

public sealed partial class RadarPulseProductPipelineService : IRadarPulseProductPipelineService
{
    public async ValueTask<RadarPulseProductQueryResult<RadarPulseProductControlSummary>> ApplyControlAsync(
        RadarPulseProductPipelineControlRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRunId(request.RunId);
        if (string.IsNullOrWhiteSpace(request.DurableStorePath))
        {
            return RadarPulseProductQueryResult<RadarPulseProductControlSummary>.NotFound(
                "Product control requires a durable store path.");
        }

        var sourceCount = request.SourceCount;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceCount);
        var universe = new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);
        var partitionCount = ResolvePartitionCount(request.PartitionCount, sourceCount);
        var shardCount = ResolveShardCount(request.ShardCount, partitionCount);
        var recoveryRequest = new RadarProcessingProductionPipelineRecoveryRequest(
            request.RunId,
            universe,
            request.DurableStorePath,
            partitionCount,
            shardCount,
            RadarPulseProductHandlerFactory.Create(request.HandlerSet),
            CreateProductionOptions(request.Options));
        var coordinator = new RadarProcessingProductionPipelineControlCoordinator();
        var result = request.Action switch
        {
            RadarPulseProductControlAction.StopAccepting =>
                coordinator.StopAccepting(recoveryRequest),
            RadarPulseProductControlAction.DrainAccepted =>
                await coordinator.DrainAcceptedAsync(recoveryRequest, cancellationToken)
                    .ConfigureAwait(false),
            RadarPulseProductControlAction.CancelOpenAndRelease =>
                coordinator.CancelOpenAndRelease(
                    recoveryRequest,
                    string.IsNullOrWhiteSpace(request.Message)
                        ? "Product control canceled open pipeline work."
                        : request.Message),
            RadarPulseProductControlAction.RejectUnsafeFallback =>
                coordinator.RejectUnsafeFallback(
                    recoveryRequest,
                    string.IsNullOrWhiteSpace(request.Message)
                        ? "Product control rejected unsafe fallback."
                        : request.Message),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };

        return RadarPulseProductQueryResult<RadarPulseProductControlSummary>.FromValue(
            RadarPulseProductPipelineMapper.ToProductControlSummary(result));
    }
}
