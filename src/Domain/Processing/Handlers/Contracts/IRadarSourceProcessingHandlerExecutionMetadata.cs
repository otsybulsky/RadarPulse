namespace RadarPulse.Domain.Processing;

/// <summary>
/// Optional metadata that classifies a handler's concurrency and merge posture.
/// </summary>
public interface IRadarSourceProcessingHandlerExecutionMetadata
{
    /// <summary>
    /// Declares whether the handler can participate in ordered delta/merge,
    /// requires snapshot fallback, or is unsupported.
    /// </summary>
    RadarSourceProcessingHandlerExecutionClassification ExecutionClassification { get; }
}
