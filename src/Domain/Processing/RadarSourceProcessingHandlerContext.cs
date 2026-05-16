using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public readonly ref struct RadarSourceProcessingHandlerContext
{
    public RadarSourceProcessingHandlerContext(
        in RadarStreamEvent streamEvent,
        ReadOnlySpan<byte> payload,
        RadarProcessingPayloadMetrics payloadMetrics)
    {
        StreamEvent = streamEvent;
        Payload = payload;
        PayloadMetrics = payloadMetrics;
    }

    public RadarStreamEvent StreamEvent { get; }

    public ReadOnlySpan<byte> Payload { get; }

    public RadarProcessingPayloadMetrics PayloadMetrics { get; }
}
