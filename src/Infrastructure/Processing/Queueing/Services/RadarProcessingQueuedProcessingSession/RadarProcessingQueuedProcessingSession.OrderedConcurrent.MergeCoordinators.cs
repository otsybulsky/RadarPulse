using System.Buffers;
using System.Diagnostics;
using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingQueuedProcessingSession
{
    private static IReadOnlyDictionary<string, RadarProcessingHandlerDeltaMergeCoordinator> CreateHandlerDeltaMergeCoordinators(
        RadarProcessingCore core)
    {
        var contract = RadarProcessingHandlerOutputContract.FromOptions(core.Options);
        if (!contract.AllowsOrderedConcurrentHandlerDeltaMerge)
        {
            throw new NotSupportedException(
                "Ordered handler delta/merge requires a mergeable handler output contract.");
        }

        var result = new Dictionary<string, RadarProcessingHandlerDeltaMergeCoordinator>(StringComparer.Ordinal);
        foreach (var descriptor in contract.Handlers)
        {
            if (core.Options.Handlers[descriptor.HandlerIndex] is not IRadarProcessingHandlerDeltaMerger merger)
            {
                throw new NotSupportedException(
                    $"Mergeable handler '{descriptor.Name}' must implement the handler delta merger contract.");
            }

            if (!string.Equals(merger.HandlerName, descriptor.Name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Mergeable handler '{descriptor.Name}' merger name does not match its descriptor.");
            }

            result.Add(
                descriptor.Name,
                new RadarProcessingHandlerDeltaMergeCoordinator(merger));
        }

        return result;
    }
}
