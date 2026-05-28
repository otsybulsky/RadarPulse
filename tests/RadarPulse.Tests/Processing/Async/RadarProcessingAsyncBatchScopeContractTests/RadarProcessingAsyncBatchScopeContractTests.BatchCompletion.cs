using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncBatchScopeContractTests
{
    [Fact]
    public void BatchCompletionCopiesCompletionCollection()
    {
        var completion = new RadarProcessingAsyncWorkCompletion(
            1,
            0,
            RadarProcessingTopologyVersion.Initial,
            new RadarProcessingWorkerId(0),
            RadarProcessingAsyncWorkStatus.Succeeded);
        var source = new List<RadarProcessingAsyncWorkCompletion> { completion };

        var batchCompletion = new RadarProcessingAsyncBatchCompletion(
            1,
            RadarProcessingTopologyVersion.Initial,
            expectedWorkItemCount: 1,
            source);
        source.Clear();

        Assert.Single(batchCompletion.Completions);
        Assert.Same(completion, batchCompletion.Completions[0]);
    }

    [Fact]
    public void BatchCompletionRejectsInvalidShapes()
    {
        var completion = new RadarProcessingAsyncWorkCompletion(
            1,
            0,
            RadarProcessingTopologyVersion.Initial,
            new RadarProcessingWorkerId(0),
            RadarProcessingAsyncWorkStatus.Succeeded);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncBatchScope(-1, RadarProcessingTopologyVersion.Initial, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncBatchScope(0, RadarProcessingTopologyVersion.Initial, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncBatchCompletion(-1, RadarProcessingTopologyVersion.Initial, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncBatchCompletion(0, RadarProcessingTopologyVersion.Initial, 0));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingAsyncBatchCompletion(
                2,
                RadarProcessingTopologyVersion.Initial,
                1,
                new[] { completion }));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingAsyncBatchCompletion(
                1,
                new RadarProcessingTopologyVersion(1),
                1,
                new[] { completion }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncBatchCompletion(
                1,
                RadarProcessingTopologyVersion.Initial,
                1,
                new[]
                {
                    new RadarProcessingAsyncWorkCompletion(
                        1,
                        1,
                        RadarProcessingTopologyVersion.Initial,
                        new RadarProcessingWorkerId(0),
                        RadarProcessingAsyncWorkStatus.Succeeded)
                }));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingAsyncBatchCompletion(
                1,
                RadarProcessingTopologyVersion.Initial,
                1,
                new[] { completion, completion }));
    }

    [Fact]
    public void ScopeResultRejectsInvalidShapes()
    {
        var completion = new RadarProcessingAsyncBatchCompletion(1, RadarProcessingTopologyVersion.Initial, 1);

        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingAsyncBatchScopeResult(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncBatchScopeResult(completion, (RadarProcessingAsyncBatchCompletionError)255));
    }
}
