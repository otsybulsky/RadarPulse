using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingProductionPipelineRunRequest
{
    private readonly IReadOnlyList<RadarEventBatch> batches;
    private readonly IReadOnlyList<IRadarSourceProcessingHandler> handlers;

    public RadarProcessingProductionPipelineRunRequest(
        string runId,
        RadarSourceUniverse sourceUniverse,
        IReadOnlyCollection<RadarEventBatch> batches,
        int partitionCount,
        int shardCount,
        IReadOnlyCollection<IRadarSourceProcessingHandler>? handlers = null,
        RadarProcessingProductionPipelineOptions? options = null,
        RadarProcessingBffReadModelStore? readModelStore = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(sourceUniverse);
        ArgumentNullException.ThrowIfNull(batches);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(partitionCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shardCount);
        if (partitionCount < shardCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(partitionCount),
                partitionCount,
                "Partition count must be greater than or equal to shard count.");
        }

        RunId = runId;
        SourceUniverse = sourceUniverse;
        this.batches = CopyRequired(batches, nameof(batches));
        PartitionCount = partitionCount;
        ShardCount = shardCount;
        this.handlers = CopyRequired(handlers ?? Array.Empty<IRadarSourceProcessingHandler>(), nameof(handlers));
        Options = options;
        ReadModelStore = readModelStore ?? new RadarProcessingBffReadModelStore();
    }

    public string RunId { get; }

    public RadarSourceUniverse SourceUniverse { get; }

    public IReadOnlyList<RadarEventBatch> Batches => batches;

    public int PartitionCount { get; }

    public int ShardCount { get; }

    public IReadOnlyList<IRadarSourceProcessingHandler> Handlers => handlers;

    public RadarProcessingProductionPipelineOptions? Options { get; }

    public RadarProcessingBffReadModelStore ReadModelStore { get; }

    private static IReadOnlyList<T> CopyRequired<T>(
        IReadOnlyCollection<T> values,
        string paramName)
        where T : class
    {
        if (values.Count == 0)
        {
            return Array.Empty<T>();
        }

        var result = new T[values.Count];
        var index = 0;
        foreach (var value in values)
        {
            result[index++] = value ?? throw new ArgumentNullException(paramName);
        }

        return Array.AsReadOnly(result);
    }
}
