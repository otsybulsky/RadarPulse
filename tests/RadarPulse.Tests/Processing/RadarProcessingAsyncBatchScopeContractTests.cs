using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingAsyncBatchScopeContractTests
{
    [Fact]
    public void AsyncBatchCompletionEnumValuesAreStable()
    {
        Assert.Equal(1, (int)RadarProcessingAsyncWorkStatus.Succeeded);
        Assert.Equal(2, (int)RadarProcessingAsyncWorkStatus.Failed);
        Assert.Equal(3, (int)RadarProcessingAsyncWorkStatus.Canceled);

        Assert.Equal(0, (int)RadarProcessingAsyncBatchCompletionError.None);
        Assert.Equal(1, (int)RadarProcessingAsyncBatchCompletionError.ScopeMismatch);
        Assert.Equal(2, (int)RadarProcessingAsyncBatchCompletionError.TopologyVersionMismatch);
        Assert.Equal(3, (int)RadarProcessingAsyncBatchCompletionError.WorkItemOutOfRange);
        Assert.Equal(4, (int)RadarProcessingAsyncBatchCompletionError.DuplicateCompletion);
        Assert.Equal(5, (int)RadarProcessingAsyncBatchCompletionError.MissingCompletion);
        Assert.Equal(6, (int)RadarProcessingAsyncBatchCompletionError.ScopeClosed);
        Assert.Equal(7, (int)RadarProcessingAsyncBatchCompletionError.WorkFailed);
        Assert.Equal(8, (int)RadarProcessingAsyncBatchCompletionError.WorkCanceled);
    }

    [Fact]
    public void ScopeCreatesWorkItemWithCopiedPartitionIds()
    {
        var scope = new RadarProcessingAsyncBatchScope(7, new RadarProcessingTopologyVersion(3), 2);
        var partitionIds = new[] { 0, 2, 4 };

        var item = scope.CreateWorkItem(
            workItemId: 1,
            new RadarProcessingWorkerId(5),
            shardId: 2,
            partitionIds);
        partitionIds[0] = 99;

        Assert.Equal(7, item.BatchSequence);
        Assert.Equal(1, item.WorkItemId);
        Assert.Equal(new RadarProcessingTopologyVersion(3), item.TopologyVersion);
        Assert.Equal(5, item.WorkerId.Value);
        Assert.Equal(2, item.ShardId);
        Assert.Equal(new[] { 0, 2, 4 }, item.PartitionIds);
        Assert.Equal(3, item.PartitionCount);
    }

    [Fact]
    public void WorkItemRejectsInvalidShapes()
    {
        var topologyVersion = RadarProcessingTopologyVersion.Initial;
        var workerId = new RadarProcessingWorkerId(0);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkItem(-1, 0, topologyVersion, workerId, 0, new[] { 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkItem(0, -1, topologyVersion, workerId, 0, new[] { 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkItem(0, 0, topologyVersion, workerId, -1, new[] { 0 }));
        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingAsyncWorkItem(0, 0, topologyVersion, workerId, 0, null!));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingAsyncWorkItem(0, 0, topologyVersion, workerId, 0, Array.Empty<int>()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkItem(0, 0, topologyVersion, workerId, 0, new[] { -1 }));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingAsyncWorkItem(0, 0, topologyVersion, workerId, 0, new[] { 1, 1 }));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingAsyncWorkItem(0, 0, topologyVersion, workerId, 0, new[] { 2, 1 }));
    }

    [Fact]
    public void CompletionFactoriesCarryWorkItemTimingAndMetrics()
    {
        var scope = new RadarProcessingAsyncBatchScope(3, new RadarProcessingTopologyVersion(2), 1);
        var item = scope.CreateWorkItem(0, new RadarProcessingWorkerId(1), shardId: 0, new[] { 0 });

        var completion = RadarProcessingAsyncWorkCompletion.Succeeded(
            item,
            queueWaitTime: TimeSpan.FromMilliseconds(2),
            executionTime: TimeSpan.FromMilliseconds(5),
            processedStreamEventCount: 7,
            processedPayloadValueCount: 11);

        Assert.Equal(item.BatchSequence, completion.BatchSequence);
        Assert.Equal(item.WorkItemId, completion.WorkItemId);
        Assert.Equal(item.TopologyVersion, completion.TopologyVersion);
        Assert.Equal(item.WorkerId, completion.WorkerId);
        Assert.Equal(RadarProcessingAsyncWorkStatus.Succeeded, completion.Status);
        Assert.True(completion.IsSuccessful);
        Assert.False(completion.IsFailed);
        Assert.False(completion.IsCanceled);
        Assert.Equal(TimeSpan.FromMilliseconds(2), completion.QueueWaitTime);
        Assert.Equal(TimeSpan.FromMilliseconds(5), completion.ExecutionTime);
        Assert.Equal(7, completion.ProcessedStreamEventCount);
        Assert.Equal(11, completion.ProcessedPayloadValueCount);
    }

    [Fact]
    public void CompletionRejectsInvalidShapes()
    {
        var topologyVersion = RadarProcessingTopologyVersion.Initial;
        var workerId = new RadarProcessingWorkerId(0);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkCompletion(-1, 0, topologyVersion, workerId, RadarProcessingAsyncWorkStatus.Succeeded));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkCompletion(0, -1, topologyVersion, workerId, RadarProcessingAsyncWorkStatus.Succeeded));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkCompletion(0, 0, topologyVersion, workerId, (RadarProcessingAsyncWorkStatus)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkCompletion(
                0,
                0,
                topologyVersion,
                workerId,
                RadarProcessingAsyncWorkStatus.Succeeded,
                queueWaitTime: TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkCompletion(
                0,
                0,
                topologyVersion,
                workerId,
                RadarProcessingAsyncWorkStatus.Succeeded,
                executionTime: TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkCompletion(
                0,
                0,
                topologyVersion,
                workerId,
                RadarProcessingAsyncWorkStatus.Succeeded,
                processedStreamEventCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkCompletion(
                0,
                0,
                topologyVersion,
                workerId,
                RadarProcessingAsyncWorkStatus.Succeeded,
                processedPayloadValueCount: -1));
        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingAsyncWorkCompletion.Succeeded(null!));
    }

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

    private static void AssertFailure(
        RadarProcessingAsyncBatchScopeResult result,
        RadarProcessingAsyncBatchCompletionError error)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(error, result.Error);
    }
}
