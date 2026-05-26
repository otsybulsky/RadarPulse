namespace RadarPulse.Domain.Processing;

public interface IRadarProcessingHandlerDeltaMerger
{
    string HandlerName { get; }

    string HandlerContractVersion { get; }

    IReadOnlyList<RadarProcessingHandlerDeltaValue> Merge(
        IReadOnlyList<RadarProcessingHandlerDeltaValue> currentValues,
        RadarProcessingHandlerDelta delta);
}
