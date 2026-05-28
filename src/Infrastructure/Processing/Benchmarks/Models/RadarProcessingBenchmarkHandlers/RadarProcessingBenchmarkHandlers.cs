using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Factory for deterministic benchmark handlers used by synthetic processing runs.
/// </summary>
public static partial class RadarProcessingBenchmarkHandlers
{
    /// <summary>
    /// Creates handlers for the selected benchmark handler set.
    /// </summary>
    public static IReadOnlyList<IRadarSourceProcessingHandler> Create(
        RadarProcessingBenchmarkHandlerSet handlerSet) =>
        handlerSet switch
        {
            RadarProcessingBenchmarkHandlerSet.None => Array.Empty<IRadarSourceProcessingHandler>(),
            RadarProcessingBenchmarkHandlerSet.CounterChecksum =>
                new IRadarSourceProcessingHandler[] { new CounterChecksumBenchmarkHandler() },
            RadarProcessingBenchmarkHandlerSet.CounterChecksumHeavy =>
                new IRadarSourceProcessingHandler[]
                {
                    new CounterChecksumBenchmarkHandler(),
                    new HeavySampledChecksumBenchmarkHandler()
                },
            _ => throw new ArgumentOutOfRangeException(nameof(handlerSet))
        };

    /// <summary>
    /// Ensures the benchmark handler set is a known value.
    /// </summary>
    public static void EnsureKnown(
        RadarProcessingBenchmarkHandlerSet handlerSet)
    {
        if (handlerSet is not RadarProcessingBenchmarkHandlerSet.None and
            not RadarProcessingBenchmarkHandlerSet.CounterChecksum and
            not RadarProcessingBenchmarkHandlerSet.CounterChecksumHeavy)
        {
            throw new ArgumentOutOfRangeException(nameof(handlerSet));
        }
    }
}
