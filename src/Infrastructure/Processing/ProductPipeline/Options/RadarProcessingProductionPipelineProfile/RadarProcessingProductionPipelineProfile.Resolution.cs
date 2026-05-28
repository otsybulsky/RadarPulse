using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public static partial class RadarProcessingProductionPipelineProfile
{
    private static RadarProcessingProductionPipelineResolvedOption<T> Resolve<T>(
        T? value,
        T defaultValue,
        RadarProcessingProductionPipelineOptionSource source)
        where T : struct =>
        value.HasValue
            ? new RadarProcessingProductionPipelineResolvedOption<T>(value.Value, source)
            : new RadarProcessingProductionPipelineResolvedOption<T>(
                defaultValue,
                RadarProcessingProductionPipelineOptionSource.Profile);

    private static RadarProcessingProductionPipelineResolvedOption<int?> ResolveNullable(
        int? value,
        RadarProcessingProductionPipelineOptionSource source) =>
        value.HasValue
            ? new RadarProcessingProductionPipelineResolvedOption<int?>(value.Value, source)
            : new RadarProcessingProductionPipelineResolvedOption<int?>(
                null,
                RadarProcessingProductionPipelineOptionSource.Default);

    private static RadarProcessingProductionPipelineOptionSource NormalizeOverrideSource(
        RadarProcessingProductionPipelineOptionSource source) =>
        source is RadarProcessingProductionPipelineOptionSource.ExplicitOverride or
            RadarProcessingProductionPipelineOptionSource.TestHarness
            ? source
            : RadarProcessingProductionPipelineOptionSource.ExplicitOverride;

    private static void AddOverrideWarning<T>(
        RadarProcessingProductionPipelineResolvedOption<T> option,
        T acceptedDefault,
        List<string> warnings)
    {
        if (option.Source == RadarProcessingProductionPipelineOptionSource.Profile ||
            option.Source == RadarProcessingProductionPipelineOptionSource.Default ||
            EqualityComparer<T>.Default.Equals(option.Value, acceptedDefault))
        {
            return;
        }

        warnings.Add(
            $"Explicit production pipeline override changes accepted default to {option.Value}.");
    }
}
