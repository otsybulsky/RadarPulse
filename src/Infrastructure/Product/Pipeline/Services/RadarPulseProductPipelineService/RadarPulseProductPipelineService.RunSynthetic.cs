using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Infrastructure.Product;

public sealed partial class RadarPulseProductPipelineService : IRadarPulseProductPipelineService
{
    public async ValueTask<RadarPulseProductRunDetail> RunSyntheticAsync(
        RadarPulseProductPipelineSyntheticRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRunId(request.RunId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.SourceCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.BatchCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.EventsPerBatch);

        var universe = new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: request.SourceCount,
            rangeBandCount: 1);
        var partitionCount = ResolvePartitionCount(request.PartitionCount, request.SourceCount);
        var shardCount = ResolveShardCount(request.ShardCount, partitionCount);
        var batches = RadarPulseProductSyntheticBatchFactory.CreateBatches(
            universe,
            request.BatchCount,
            request.EventsPerBatch);
        var input = new RadarPulseProductInputSummary(
            RadarPulseProductInputKind.Synthetic,
            "deterministic synthetic product pipeline input",
            "synthetic",
            request.BatchCount,
            checked((long)request.BatchCount * request.EventsPerBatch));

        return await RunBatchesAsync(
                request.RunId,
                universe,
                batches,
                partitionCount,
                shardCount,
                request.HandlerSet,
                request.Options,
                input,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Runs the accepted production-shaped pipeline over one local NEXRAD archive file.
    /// </summary>
    /// <remarks>
    /// The archive publisher projects the local file into owned RadarEventBatch
    /// input before processing. The method rejects archive files that produce no
}
