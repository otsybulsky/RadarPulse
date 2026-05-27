namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingProductionPipelineResolvedOption<T>(
    T Value,
    RadarProcessingProductionPipelineOptionSource Source);
