using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Infrastructure.Product;

public sealed partial class RadarPulseProductPipelineService : IRadarPulseProductPipelineService
{
    public async ValueTask<RadarPulseProductRunDetail> RunArchiveFileAsync(
        RadarPulseProductPipelineArchiveFileRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRunId(request.RunId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FilePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Parallelism);

        var sourceUniverse = ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse;
        var publisher = new CapturingArchiveRadarEventBatchPublisher();
        var archiveOptions = new ArchiveRadarEventBatchPublishOptions(
            sourceUniverse,
            request.Parallelism);
        var archivePublisher = new NexradArchiveRadarEventBatchPublisher(
            ArchiveBZip2Decompressors.Create(request.Decompressor));
        var archiveResult = archivePublisher.PublishFile(
            request.FilePath,
            publisher,
            archiveOptions,
            cancellationToken);
        var batches = publisher.Batches;
        if (batches.Count == 0)
        {
            throw new InvalidOperationException("Archive file did not produce any RadarEventBatch input.");
        }

        var partitionCount = ResolvePartitionCount(
            request.PartitionCount,
            sourceUniverse.SourceCount);
        var shardCount = ResolveShardCount(request.ShardCount, partitionCount);
        var input = new RadarPulseProductInputSummary(
            RadarPulseProductInputKind.ArchiveFile,
            "NEXRAD archive file product pipeline input",
            Path.GetFullPath(request.FilePath),
            batches.Count,
            archiveResult.EventCount);

        return await RunBatchesAsync(
                request.RunId,
                sourceUniverse,
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
}
