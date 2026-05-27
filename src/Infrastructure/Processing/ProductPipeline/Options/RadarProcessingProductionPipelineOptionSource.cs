namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Source of a resolved production-pipeline option value.
/// </summary>
public enum RadarProcessingProductionPipelineOptionSource
{
    /// <summary>
    /// Value came from a generic default rather than the named profile.
    /// </summary>
    Default = 1,

    /// <summary>
    /// Value came from the accepted production-pipeline profile.
    /// </summary>
    Profile = 2,

    /// <summary>
    /// Value came from an explicit caller override.
    /// </summary>
    ExplicitOverride = 3,

    /// <summary>
    /// Value came from a test harness override.
    /// </summary>
    TestHarness = 4
}
