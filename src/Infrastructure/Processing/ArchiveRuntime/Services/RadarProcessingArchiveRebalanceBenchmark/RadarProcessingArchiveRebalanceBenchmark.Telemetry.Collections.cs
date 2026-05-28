using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveRebalanceBenchmark
{
    private static IReadOnlyList<T> CreateReadOnlyList<T>(List<T>? values) =>
        values is { Count: > 0 }
            ? Array.AsReadOnly(values.ToArray())
            : Array.Empty<T>();

    private static IReadOnlyList<RadarProcessingRebalanceSkippedReasonCounter> CreateSortedSkippedReasonCounters(
        List<RadarProcessingRebalanceSkippedReasonCounter>? values)
    {
        if (values is not { Count: > 0 })
        {
            return Array.Empty<RadarProcessingRebalanceSkippedReasonCounter>();
        }

        var result = values.ToArray();
        Array.Sort(result, (left, right) => left.Reason.CompareTo(right.Reason));
        return Array.AsReadOnly(result);
    }
}
