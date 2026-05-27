namespace RadarPulse.Domain.Processing;

public interface IRadarSourceProcessingHandler
{
    RadarSourceProcessingHandlerDescriptor Descriptor { get; }

    void Process(
        in RadarSourceProcessingHandlerContext context,
        RadarSourceProcessingState state);
}
