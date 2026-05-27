namespace RadarPulse.Domain.Processing;

/// <summary>
/// Extension point for per-source processing logic executed while radar events are applied.
/// </summary>
/// <remarks>
/// Handlers write only to the supplied source-local state slots declared by
/// <see cref="Descriptor"/>. Implementations must not retain references to the
/// ref-like context or state beyond the call.
/// </remarks>
public interface IRadarSourceProcessingHandler
{
    /// <summary>
    /// Declares the handler name, slot counts, and exported snapshot fields.
    /// </summary>
    RadarSourceProcessingHandlerDescriptor Descriptor { get; }

    /// <summary>
    /// Applies one stream event and payload to the source-local handler state.
    /// </summary>
    void Process(
        in RadarSourceProcessingHandlerContext context,
        RadarSourceProcessingState state);
}
