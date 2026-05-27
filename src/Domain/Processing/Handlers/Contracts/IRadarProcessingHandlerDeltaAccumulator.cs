namespace RadarPulse.Domain.Processing;

public interface IRadarProcessingHandlerDeltaAccumulator
{
    IReadOnlyList<RadarProcessingHandlerDeltaValue> Merge(
        RadarProcessingHandlerDelta delta);

    IReadOnlyList<RadarProcessingHandlerDeltaValue> CreateMergedValuesSnapshot();
}
