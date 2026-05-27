using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingHandlerDeltaClassificationTests
{
    [Fact]
    public void HandlerFreeContractRemainsOrderedConcurrentEligible()
    {
        var contract = RadarProcessingHandlerOutputContract.FromHandlers(null);

        Assert.Equal(
            RadarProcessingHandlerStatePosture.HandlerFreeOrderedConcurrent,
            contract.StatePosture);
        Assert.False(contract.HasHandlers);
        Assert.True(contract.AllowsOrderedConcurrentDelta);
        Assert.False(contract.AllowsOrderedConcurrentHandlerDeltaMerge);
        Assert.False(contract.RequiresSequentialFallback);
        Assert.False(contract.IsUnsupported);
        Assert.Null(contract.FirstBlockingReason);
    }

    [Fact]
    public void ExistingStatefulHandlersDefaultToSnapshotOnlyFallback()
    {
        var contract = RadarProcessingHandlerOutputContract.FromHandlers(
            new IRadarSourceProcessingHandler[] { new ClassifiedCountingHandler() });

        Assert.Equal(
            RadarProcessingHandlerStatePosture.StatefulSnapshotSequentialFallback,
            contract.StatePosture);
        Assert.True(contract.HasHandlers);
        Assert.True(contract.RequiresSequentialFallback);
        Assert.False(contract.AllowsOrderedConcurrentDelta);
        Assert.False(contract.AllowsOrderedConcurrentHandlerDeltaMerge);
        Assert.Null(contract.FirstBlockingReason);
        Assert.Contains("counting", contract.Message, StringComparison.Ordinal);

        var handler = Assert.Single(contract.Handlers);
        Assert.Equal(
            RadarSourceProcessingHandlerExecutionClassification.SnapshotOnly,
            handler.ExecutionClassification);
    }

    [Fact]
    public void AllMergeableHandlerSetIsDeltaMergeEligible()
    {
        var contract = RadarProcessingHandlerOutputContract.FromHandlers(
            new IRadarSourceProcessingHandler[]
            {
                new ClassifiedCountingHandler(
                    "first",
                    RadarSourceProcessingHandlerExecutionClassification.Mergeable),
                new ClassifiedCountingHandler(
                    "second",
                    RadarSourceProcessingHandlerExecutionClassification.Mergeable,
                    eventFieldName: "second.events")
            });

        Assert.Equal(
            RadarProcessingHandlerStatePosture.MergeableHandlerDeltaMergeEligible,
            contract.StatePosture);
        Assert.True(contract.HasHandlers);
        Assert.False(contract.RequiresSequentialFallback);
        Assert.False(contract.AllowsOrderedConcurrentDelta);
        Assert.True(contract.AllowsOrderedConcurrentHandlerDeltaMerge);
        Assert.Null(contract.FirstBlockingReason);
        Assert.All(
            contract.Handlers,
            handler => Assert.Equal(
                RadarSourceProcessingHandlerExecutionClassification.Mergeable,
                handler.ExecutionClassification));
    }

    [Fact]
    public void MixedMergeableAndSnapshotOnlyHandlerSetSelectsSequentialFallback()
    {
        var contract = RadarProcessingHandlerOutputContract.FromHandlers(
            new IRadarSourceProcessingHandler[]
            {
                new ClassifiedCountingHandler(
                    "mergeable",
                    RadarSourceProcessingHandlerExecutionClassification.Mergeable),
                new ClassifiedCountingHandler(
                    "snapshot",
                    RadarSourceProcessingHandlerExecutionClassification.SnapshotOnly,
                    eventFieldName: "snapshot.events")
            });

        Assert.Equal(
            RadarProcessingHandlerStatePosture.StatefulSnapshotSequentialFallback,
            contract.StatePosture);
        Assert.True(contract.RequiresSequentialFallback);
        Assert.False(contract.AllowsOrderedConcurrentHandlerDeltaMerge);
        Assert.Null(contract.FirstBlockingReason);
        Assert.Contains("snapshot", contract.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsupportedHandlerSetFailsClosedWithBlockingReason()
    {
        var contract = RadarProcessingHandlerOutputContract.FromHandlers(
            new IRadarSourceProcessingHandler[]
            {
                new ClassifiedCountingHandler(
                    "mergeable",
                    RadarSourceProcessingHandlerExecutionClassification.Mergeable),
                new ClassifiedCountingHandler(
                    "unsupported",
                    RadarSourceProcessingHandlerExecutionClassification.Unsupported,
                    eventFieldName: "unsupported.events")
            });

        Assert.Equal(
            RadarProcessingHandlerStatePosture.UnsupportedHandlerSet,
            contract.StatePosture);
        Assert.True(contract.IsUnsupported);
        Assert.False(contract.RequiresSequentialFallback);
        Assert.False(contract.AllowsOrderedConcurrentDelta);
        Assert.False(contract.AllowsOrderedConcurrentHandlerDeltaMerge);
        Assert.Contains("Unsupported handler 'unsupported'", contract.FirstBlockingReason, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidHandlerClassificationIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingHandlerOutputContract.FromHandlers(
                new IRadarSourceProcessingHandler[]
                {
                    new ClassifiedCountingHandler(
                        "invalid",
                        (RadarSourceProcessingHandlerExecutionClassification)42)
                }));
    }

    private sealed class ClassifiedCountingHandler :
        IRadarSourceProcessingHandler,
        IRadarSourceProcessingHandlerExecutionMetadata
    {
        public ClassifiedCountingHandler(
            string name = "counting",
            RadarSourceProcessingHandlerExecutionClassification? executionClassification = null,
            string eventFieldName = "events")
        {
            Descriptor = new RadarSourceProcessingHandlerDescriptor(
                name,
                int64SlotCount: 1,
                doubleSlotCount: 0,
                new[]
                {
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        eventFieldName,
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 0)
                });
            ExecutionClassification = executionClassification ??
                                      RadarSourceProcessingHandlerExecutionClassification.SnapshotOnly;
        }

        public RadarSourceProcessingHandlerDescriptor Descriptor { get; }

        public RadarSourceProcessingHandlerExecutionClassification ExecutionClassification { get; }

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            state.AddInt64(slotIndex: 0, value: 1);
        }
    }
}
