using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingOrderedResultCoordinatorTests
{
    [Fact]
    public void OutOfOrderSuccessfulCompletionsPublishInSequenceOrder()
    {
        var coordinator = new RadarProcessingOrderedResultCoordinator();

        var delayed = coordinator.Complete(Succeeded(sequence: 1));
        var published = coordinator.Complete(Succeeded(sequence: 0));

        Assert.Empty(delayed);
        Assert.Equal([0L, 1L], published.Select(static result => result.Sequence.Value).ToArray());
        Assert.Equal(2, coordinator.NextPublishSequence);
        Assert.Equal(0, coordinator.PendingCount);
        Assert.False(coordinator.HasPublishedTerminalFailure);
    }

    [Fact]
    public void LaterCompletionWaitsBehindIncompleteEarlierSequence()
    {
        var coordinator = new RadarProcessingOrderedResultCoordinator();

        Assert.Empty(coordinator.Complete(Succeeded(sequence: 2)));
        Assert.Equal([0L], coordinator.Complete(Succeeded(sequence: 0)).Select(static result => result.Sequence.Value));
        Assert.Equal(1, coordinator.NextPublishSequence);
        Assert.Equal(1, coordinator.PendingCount);

        var published = coordinator.Complete(Succeeded(sequence: 1));

        Assert.Equal([1L, 2L], published.Select(static result => result.Sequence.Value).ToArray());
        Assert.Equal(3, coordinator.NextPublishSequence);
        Assert.Equal(0, coordinator.PendingCount);
    }

    [Fact]
    public void EarlierFailureBlocksLaterUnpublishedSuccess()
    {
        var coordinator = new RadarProcessingOrderedResultCoordinator();

        Assert.Empty(coordinator.Complete(Succeeded(sequence: 1)));
        var published = coordinator.Complete(
            RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                new RadarProcessingQueuedBatchSequence(0),
                "processing failed"));

        var result = Assert.Single(published);
        Assert.Equal(0, result.Sequence.Value);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.FailedProcessing, result.Status);
        Assert.True(coordinator.HasPublishedTerminalFailure);
        Assert.True(coordinator.IsBlockedByTerminalFailure);
        Assert.Equal(1, coordinator.NextPublishSequence);
        Assert.Equal(1, coordinator.PendingCount);
    }

    [Fact]
    public void CanceledAndSkippedRecordsPublishAfterTerminalFailureInOrder()
    {
        var coordinator = new RadarProcessingOrderedResultCoordinator();

        Assert.Equal(
            [0L],
            coordinator.Complete(
                    RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        new RadarProcessingQueuedBatchSequence(0),
                        "validation failed"))
                .Select(static result => result.Sequence.Value));

        Assert.Equal(
            [1L],
            coordinator.Complete(
                    RadarProcessingQueuedBatchProcessingResult.Canceled(
                        new RadarProcessingQueuedBatchSequence(1),
                        "canceled"))
                .Select(static result => result.Sequence.Value));

        Assert.Equal(
            [2L],
            coordinator.Complete(
                    RadarProcessingQueuedBatchProcessingResult.SkippedAfterFault(
                        new RadarProcessingQueuedBatchSequence(2),
                        "skipped"))
                .Select(static result => result.Sequence.Value));
        Assert.False(coordinator.IsBlockedByTerminalFailure);
        Assert.Equal(3, coordinator.NextPublishSequence);
    }

    [Fact]
    public void DuplicateOrAlreadyPublishedSequenceIsRejected()
    {
        var coordinator = new RadarProcessingOrderedResultCoordinator();

        coordinator.Complete(Succeeded(sequence: 1));
        Assert.Throws<InvalidOperationException>(() => coordinator.Complete(Succeeded(sequence: 1)));

        coordinator.Complete(Succeeded(sequence: 0));
        Assert.Throws<InvalidOperationException>(() => coordinator.Complete(Succeeded(sequence: 0)));
    }

    private static RadarProcessingQueuedBatchProcessingResult Succeeded(long sequence) =>
        RadarProcessingQueuedBatchProcessingResult.Succeeded(
            new RadarProcessingQueuedBatchSequence(sequence),
            RadarProcessingResult.Empty(
                new RadarProcessingCoreOptions(
                    RadarProcessingExecutionMode.Sequential,
                    partitionCount: 1,
                    shardCount: 1)));
}
