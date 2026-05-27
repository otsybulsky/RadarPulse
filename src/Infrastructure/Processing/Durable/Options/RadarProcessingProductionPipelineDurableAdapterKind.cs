namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Durable persistence adapter selected by production-pipeline options.
/// </summary>
public enum RadarProcessingProductionPipelineDurableAdapterKind
{
    /// <summary>
    /// Local JSON file durable adapter.
    /// </summary>
    File = 1
}
