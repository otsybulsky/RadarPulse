using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingMvpHandlerDeltaRuntimeTests
{
    [Fact]
    public void AllMergeableMvpPlanUsesOrderedDeltaMergeProvenance()
    {
        var core = CreateCore(CreateUniverse(sourceCount: 2), new MergeableCountingHandler());
        var requested = new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 4);

        var plan = RadarProcessingMvpRuntimePlan.Create(core, requested);

        Assert.False(plan.UsedSequentialFallback);
        Assert.False(plan.AllowsOrderedConcurrentDelta);
        Assert.True(plan.AllowsOrderedConcurrentHandlerDeltaMerge);
        Assert.Same(requested, plan.EffectiveOrderedConcurrencyOptions);
        Assert.True(plan.HandlerOutputContract.AllowsOrderedConcurrentHandlerDeltaMerge);
        Assert.Contains("delta/merge", plan.Message, StringComparison.Ordinal);
    }
}
