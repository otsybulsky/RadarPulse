using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Input contract for recovery or control actions over a durable production-pipeline store.
/// </summary>
public sealed class RadarProcessingProductionPipelineRecoveryRequest
{
    private readonly IReadOnlyList<IRadarSourceProcessingHandler> handlers;

    /// <summary>
    /// Creates a recovery request for a file-backed durable store.
    /// </summary>
    public RadarProcessingProductionPipelineRecoveryRequest(
        string runId,
        RadarSourceUniverse sourceUniverse,
        string durableStorePath,
        int partitionCount,
        int shardCount,
        IReadOnlyCollection<IRadarSourceProcessingHandler>? handlers = null,
        RadarProcessingProductionPipelineOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(sourceUniverse);
        ArgumentException.ThrowIfNullOrWhiteSpace(durableStorePath);
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
        DurableStorePath = Path.GetFullPath(durableStorePath);
        PartitionCount = partitionCount;
        ShardCount = shardCount;
        this.handlers = CopyRequired(handlers ?? Array.Empty<IRadarSourceProcessingHandler>(), nameof(handlers));
        Options = options;
    }

    /// <summary>
    /// Stable run id associated with the durable store.
    /// </summary>
    public string RunId { get; }

    /// <summary>
    /// Source universe required to rebuild the processing core for recovery.
    /// </summary>
    public RadarSourceUniverse SourceUniverse { get; }

    /// <summary>
    /// Absolute durable store path used for recovery or control.
    /// </summary>
    public string DurableStorePath { get; }

    /// <summary>
    /// Processing partition count.
    /// </summary>
    public int PartitionCount { get; }

    /// <summary>
    /// Processing shard count.
    /// </summary>
    public int ShardCount { get; }

    /// <summary>
    /// Optional custom source handlers required to rebuild the core.
    /// </summary>
    public IReadOnlyList<IRadarSourceProcessingHandler> Handlers => handlers;

    /// <summary>
    /// Optional production-pipeline profile overrides.
    /// </summary>
    public RadarProcessingProductionPipelineOptions? Options { get; }

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
