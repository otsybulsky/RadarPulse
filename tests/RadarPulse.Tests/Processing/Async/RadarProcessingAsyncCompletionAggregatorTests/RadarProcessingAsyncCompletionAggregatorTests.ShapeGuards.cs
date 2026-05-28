using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncCompletionAggregatorTests
{
    [Fact]
    public void AggregationContractsRejectInvalidShapes()
    {
        using var group = CreateStartedGroup(workerCount: 2, queueCapacity: 1);
        var plan = CreatePlan(group);
        var dispatchResult = CreateDispatchResult(
            plan,
            CreateBatchResult(
                plan,
                new[]
                {
                    CreateSucceededCompletion(plan, workItemId: 0),
                    CreateSucceededCompletion(plan, workItemId: 1)
                }));
        var telemetry = RadarProcessingTelemetry.FromRoute(
            RadarProcessingExecutionMode.AsyncShardTransport,
            plan.Route);

        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingAsyncCompletionAggregator().Aggregate(null!));
        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingAsyncAggregationResult(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncAggregationResult(dispatchResult, (RadarProcessingAsyncAggregationError)255));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingAsyncAggregationResult(dispatchResult));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingAsyncAggregationResult(
                dispatchResult,
                RadarProcessingAsyncAggregationError.WorkFailed,
                telemetry));
    }
}
