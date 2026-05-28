using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

/// <summary>
/// Applies radar event batches to source state and produces deterministic processing metrics.
/// </summary>
/// <remarks>
/// The core owns source state and topology manager state. Async shard transport callers compute
/// work outside the core but still commit through the same validation and state application paths.
/// </remarks>
public sealed partial class RadarProcessingCore
{
    private readonly RadarSourceUniverse sourceUniverse;
    private readonly RadarProcessingTopologyManager topologyManager;
    private readonly RadarSourceProcessingStateStore stateStore;
    private readonly object asyncHandlerStateSync = new();
    private long processedBatchCount;

    /// <summary>
    /// Creates a processing core for a source universe and optional execution configuration.
    /// </summary>
    public RadarProcessingCore(
        RadarSourceUniverse sourceUniverse,
        RadarProcessingCoreOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sourceUniverse);

        options ??= RadarProcessingCoreOptions.Default;

        this.sourceUniverse = sourceUniverse;
        Options = options;
        topologyManager = new RadarProcessingTopologyManager(sourceUniverse, options);
        stateStore = new RadarSourceProcessingStateStore(sourceUniverse, options.HandlerSlotLayout);
    }

    /// <summary>
    /// Gets the immutable configuration used by this core.
    /// </summary>
    public RadarProcessingCoreOptions Options { get; }

    /// <summary>
    /// Gets the current processing topology.
    /// </summary>
    public RadarProcessingTopology Topology => topologyManager.Current;

    internal RadarProcessingTopologyManager TopologyManager => topologyManager;

    internal RadarSourceUniverse SourceUniverse => sourceUniverse;

    /// <summary>
    /// Gets the number of sources in the configured source universe.
    /// </summary>
    public int SourceCount => sourceUniverse.SourceCount;
}
