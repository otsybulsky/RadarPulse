using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Input contract for one production-pipeline run over prepared radar event batches.
/// </summary>
/// <remarks>
/// The request copies batch and handler collections and requires partition count
/// to cover shard count. It does not create batches; callers provide owned or
/// archive-shaped batch input for the runner to publish.
/// </remarks>
public sealed class RadarProcessingProductionPipelineRunRequest
{
    private readonly IReadOnlyList<RadarEventBatch> batches;
    private readonly IReadOnlyList<IRadarSourceProcessingHandler> handlers;

    /// <summary>
    /// Creates a production-pipeline run request.
    /// </summary>
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

    /// <summary>
    /// Stable run id used in product and read-model output.
    /// </summary>
    public string RunId { get; }

    /// <summary>
    /// Source universe used to validate batch identity versions and build read models.
    /// </summary>
    public RadarSourceUniverse SourceUniverse { get; }

    /// <summary>
    /// Prepared input batches for the production pipeline.
    /// </summary>
    public IReadOnlyList<RadarEventBatch> Batches => batches;

    /// <summary>
    /// Processing partition count.
    /// </summary>
    public int PartitionCount { get; }

    /// <summary>
    /// Processing shard count.
    /// </summary>
    public int ShardCount { get; }

    /// <summary>
    /// Optional custom source handlers.
    /// </summary>
    public IReadOnlyList<IRadarSourceProcessingHandler> Handlers => handlers;

    /// <summary>
    /// Optional production-pipeline profile overrides.
    /// </summary>
    public RadarProcessingProductionPipelineOptions? Options { get; }

    /// <summary>
    /// Read model store that receives published product-facing evidence.
    /// </summary>
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
