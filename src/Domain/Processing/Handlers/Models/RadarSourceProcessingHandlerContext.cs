using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

/// <summary>
/// Ref-like event context passed to a source processing handler.
/// </summary>
/// <remarks>
/// The payload span is valid only for the duration of the handler call. Handlers
/// must copy any data they need to retain.
/// </remarks>
public readonly ref struct RadarSourceProcessingHandlerContext
{
    /// <summary>
    /// Creates handler context for one stream event and payload.
    /// </summary>
    public RadarSourceProcessingHandlerContext(
        in RadarStreamEvent streamEvent,
        ReadOnlySpan<byte> payload,
        RadarProcessingPayloadMetrics payloadMetrics)
    {
        StreamEvent = streamEvent;
        Payload = payload;
        PayloadMetrics = payloadMetrics;
    }

    /// <summary>
    /// Stream event currently being processed.
    /// </summary>
    public RadarStreamEvent StreamEvent { get; }

    /// <summary>
    /// Event payload span valid only during the handler call.
    /// </summary>
    public ReadOnlySpan<byte> Payload { get; }

    /// <summary>
    /// Precomputed payload metrics for the current event.
    /// </summary>
    public RadarProcessingPayloadMetrics PayloadMetrics { get; }
}
