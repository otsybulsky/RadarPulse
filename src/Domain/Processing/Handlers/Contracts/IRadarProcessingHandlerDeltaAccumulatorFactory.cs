namespace RadarPulse.Domain.Processing;

public interface IRadarProcessingHandlerDeltaAccumulatorFactory
{
    IRadarProcessingHandlerDeltaAccumulator CreateAccumulator();
}
