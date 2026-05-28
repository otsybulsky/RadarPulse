using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncBatchScopeContractTests
{
    [Fact]
    public void ScopeRecordsSuccessfulCompletionsAndCompletesBatch()
    {
        var scope = new RadarProcessingAsyncBatchScope(1, RadarProcessingTopologyVersion.Initial, 2);
        var first = scope.CreateWorkItem(0, new RadarProcessingWorkerId(0), shardId: 0, new[] { 0 });
        var second = scope.CreateWorkItem(1, new RadarProcessingWorkerId(1), shardId: 1, new[] { 1 });

        var firstResult = scope.RecordCompletion(
            RadarProcessingAsyncWorkCompletion.Succeeded(
                first,
                queueWaitTime: TimeSpan.FromMilliseconds(1),
                executionTime: TimeSpan.FromMilliseconds(3),
                processedStreamEventCount: 5,
                processedPayloadValueCount: 8));
        var secondResult = scope.RecordCompletion(
            RadarProcessingAsyncWorkCompletion.Succeeded(
                second,
                queueWaitTime: TimeSpan.FromMilliseconds(2),
                executionTime: TimeSpan.FromMilliseconds(4),
                processedStreamEventCount: 13,
                processedPayloadValueCount: 21));
        var completed = scope.Complete();

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.True(completed.IsSuccess);
        Assert.True(scope.IsClosed);
        Assert.True(completed.Completion.IsClosed);
        Assert.True(completed.Completion.IsComplete);
        Assert.True(completed.Completion.IsSuccessful);
        Assert.Equal(2, completed.Completion.SucceededWorkItemCount);
        Assert.Equal(0, completed.Completion.FailedWorkItemCount);
        Assert.Equal(0, completed.Completion.CanceledWorkItemCount);
        Assert.Equal(TimeSpan.FromMilliseconds(3), completed.Completion.TotalQueueWaitTime);
        Assert.Equal(TimeSpan.FromMilliseconds(7), completed.Completion.TotalExecutionTime);
        Assert.Equal(18, completed.Completion.ProcessedStreamEventCount);
        Assert.Equal(29, completed.Completion.ProcessedPayloadValueCount);
        Assert.Equal(new[] { 0, 1 }, completed.Completion.Completions.Select(static completion => completion.WorkItemId));
    }

    [Fact]
    public void ScopeRejectsWrongScopeTopologyOutOfRangeDuplicateAndClosedCompletions()
    {
        var scope = new RadarProcessingAsyncBatchScope(1, RadarProcessingTopologyVersion.Initial, 1);
        var item = scope.CreateWorkItem(0, new RadarProcessingWorkerId(0), shardId: 0, new[] { 0 });

        AssertFailure(
            scope.RecordCompletion(
                new RadarProcessingAsyncWorkCompletion(
                    2,
                    0,
                    RadarProcessingTopologyVersion.Initial,
                    new RadarProcessingWorkerId(0),
                    RadarProcessingAsyncWorkStatus.Succeeded)),
            RadarProcessingAsyncBatchCompletionError.ScopeMismatch);
        AssertFailure(
            scope.RecordCompletion(
                new RadarProcessingAsyncWorkCompletion(
                    1,
                    0,
                    new RadarProcessingTopologyVersion(1),
                    new RadarProcessingWorkerId(0),
                    RadarProcessingAsyncWorkStatus.Succeeded)),
            RadarProcessingAsyncBatchCompletionError.TopologyVersionMismatch);
        AssertFailure(
            scope.RecordCompletion(
                new RadarProcessingAsyncWorkCompletion(
                    1,
                    1,
                    RadarProcessingTopologyVersion.Initial,
                    new RadarProcessingWorkerId(0),
                    RadarProcessingAsyncWorkStatus.Succeeded)),
            RadarProcessingAsyncBatchCompletionError.WorkItemOutOfRange);

        Assert.True(scope.RecordCompletion(RadarProcessingAsyncWorkCompletion.Succeeded(item)).IsSuccess);
        AssertFailure(
            scope.RecordCompletion(RadarProcessingAsyncWorkCompletion.Succeeded(item)),
            RadarProcessingAsyncBatchCompletionError.DuplicateCompletion);
        Assert.True(scope.Complete().IsSuccess);
        AssertFailure(
            scope.RecordCompletion(RadarProcessingAsyncWorkCompletion.Succeeded(item)),
            RadarProcessingAsyncBatchCompletionError.ScopeClosed);
    }

    [Fact]
    public void CompleteReportsMissingCompletionWithoutClosingScope()
    {
        var scope = new RadarProcessingAsyncBatchScope(1, RadarProcessingTopologyVersion.Initial, 2);
        var item = scope.CreateWorkItem(0, new RadarProcessingWorkerId(0), shardId: 0, new[] { 0 });

        Assert.True(scope.RecordCompletion(RadarProcessingAsyncWorkCompletion.Succeeded(item)).IsSuccess);
        var result = scope.Complete();

        AssertFailure(result, RadarProcessingAsyncBatchCompletionError.MissingCompletion);
        Assert.False(scope.IsClosed);
        Assert.False(result.Completion.IsClosed);
        Assert.False(result.Completion.IsComplete);
        Assert.Equal(1, result.Completion.RecordedWorkItemCount);
    }

    [Fact]
    public void FailedWorkItemFailsCompletedBatch()
    {
        var scope = new RadarProcessingAsyncBatchScope(1, RadarProcessingTopologyVersion.Initial, 1);
        var item = scope.CreateWorkItem(0, new RadarProcessingWorkerId(0), shardId: 0, new[] { 0 });

        Assert.True(scope.RecordCompletion(RadarProcessingAsyncWorkCompletion.Failed(item)).IsSuccess);
        var result = scope.Complete();

        AssertFailure(result, RadarProcessingAsyncBatchCompletionError.WorkFailed);
        Assert.True(result.Completion.IsClosed);
        Assert.True(result.Completion.IsComplete);
        Assert.False(result.Completion.IsSuccessful);
        Assert.Equal(1, result.Completion.FailedWorkItemCount);
    }

    [Fact]
    public void CanceledWorkItemCancelsCompletedBatch()
    {
        var scope = new RadarProcessingAsyncBatchScope(1, RadarProcessingTopologyVersion.Initial, 1);
        var item = scope.CreateWorkItem(0, new RadarProcessingWorkerId(0), shardId: 0, new[] { 0 });

        Assert.True(scope.RecordCompletion(RadarProcessingAsyncWorkCompletion.Canceled(item)).IsSuccess);
        var result = scope.Complete();

        AssertFailure(result, RadarProcessingAsyncBatchCompletionError.WorkCanceled);
        Assert.True(result.Completion.IsClosed);
        Assert.True(result.Completion.IsComplete);
        Assert.False(result.Completion.IsSuccessful);
        Assert.Equal(1, result.Completion.CanceledWorkItemCount);
    }

    [Fact]
    public void CompletionSnapshotsAreImmutableAfterLaterScopeChanges()
    {
        var scope = new RadarProcessingAsyncBatchScope(1, RadarProcessingTopologyVersion.Initial, 2);
        var first = scope.CreateWorkItem(0, new RadarProcessingWorkerId(0), shardId: 0, new[] { 0 });
        var second = scope.CreateWorkItem(1, new RadarProcessingWorkerId(1), shardId: 1, new[] { 1 });

        var firstResult = scope.RecordCompletion(RadarProcessingAsyncWorkCompletion.Succeeded(first));
        Assert.True(scope.RecordCompletion(RadarProcessingAsyncWorkCompletion.Succeeded(second)).IsSuccess);
        var completed = scope.Complete();

        Assert.Equal(1, firstResult.Completion.RecordedWorkItemCount);
        Assert.False(firstResult.Completion.IsClosed);
        Assert.Equal(2, completed.Completion.RecordedWorkItemCount);
        Assert.True(completed.Completion.IsClosed);
    }
}
