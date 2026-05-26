namespace RadarPulse.Domain.Processing;

public interface IRadarSourceProcessingHandlerExecutionMetadata
{
    RadarSourceProcessingHandlerExecutionClassification ExecutionClassification { get; }
}
