using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingProviderQueueContractTests
{
    [Fact]
    public void EnqueueResultsSeparateAcceptedFromRejectedStatuses()
    {
        var queued = new RadarProcessingQueuedBatch(
            RadarProcessingQueuedBatchSequence.Initial,
            CreateOwnedBatch());

        var accepted = RadarProcessingQueuedBatchEnqueueResult.Accepted(
            queued,
            TimeSpan.FromMilliseconds(3));
        var full = RadarProcessingQueuedBatchEnqueueResult.Full(
            TimeSpan.FromMilliseconds(4),
            "queue full");
        var timedOut = RadarProcessingQueuedBatchEnqueueResult.TimedOut(
            TimeSpan.FromMilliseconds(5));
        var canceled = RadarProcessingQueuedBatchEnqueueResult.Canceled();
        var closed = RadarProcessingQueuedBatchEnqueueResult.Closed();
        var faulted = RadarProcessingQueuedBatchEnqueueResult.Faulted(message: "session faulted");

        Assert.True(accepted.IsAccepted);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Accepted, accepted.Status);
        Assert.Same(queued, accepted.Batch);
        Assert.Equal(RadarProcessingQueuedBatchSequence.Initial, accepted.Sequence);
        Assert.Equal(TimeSpan.FromMilliseconds(3), accepted.EnqueueWaitTime);

        Assert.False(full.IsAccepted);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Full, full.Status);
        Assert.Null(full.Batch);
        Assert.Null(full.Sequence);
        Assert.Equal(TimeSpan.FromMilliseconds(4), full.EnqueueWaitTime);
        Assert.Equal("queue full", full.Message);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.TimedOut, timedOut.Status);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Canceled, canceled.Status);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Closed, closed.Status);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Faulted, faulted.Status);
        Assert.Equal("session faulted", faulted.Message);

        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingQueuedBatchEnqueueResult.Accepted(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingQueuedBatchEnqueueResult.Full(TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingQueuedBatchEnqueueResult.Faulted(message: null!));
    }

    [Fact]
    public void ProcessingResultsSeparateSuccessFailureCancellationAndSkippedStatuses()
    {
        var sequence = new RadarProcessingQueuedBatchSequence(3);
        var processingResult = CreateProcessingResult(new RadarProcessingTopologyVersion(9));

        var succeeded = RadarProcessingQueuedBatchProcessingResult.Succeeded(sequence, processingResult);
        var failedProcessing = RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
            sequence.Next(),
            "processing failed",
            processingResult);
        var failedValidation = RadarProcessingQueuedBatchProcessingResult.FailedValidation(
            new RadarProcessingQueuedBatchSequence(5),
            "validation failed");
        var failedMigration = RadarProcessingQueuedBatchProcessingResult.FailedMigration(
            new RadarProcessingQueuedBatchSequence(6),
            "migration failed");
        var canceled = RadarProcessingQueuedBatchProcessingResult.Canceled(
            new RadarProcessingQueuedBatchSequence(7));
        var skipped = RadarProcessingQueuedBatchProcessingResult.SkippedAfterFault(
            new RadarProcessingQueuedBatchSequence(8),
            "already faulted");

        Assert.True(succeeded.IsSuccessful);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.Succeeded, succeeded.Status);
        Assert.Same(processingResult, succeeded.ProcessingResult);
        Assert.Equal(new RadarProcessingTopologyVersion(9), succeeded.TopologyVersion);

        Assert.False(failedProcessing.IsSuccessful);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.FailedProcessing, failedProcessing.Status);
        Assert.Same(processingResult, failedProcessing.ProcessingResult);
        Assert.Equal("processing failed", failedProcessing.Message);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.FailedValidation, failedValidation.Status);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.FailedMigration, failedMigration.Status);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.Canceled, canceled.Status);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault, skipped.Status);
        Assert.Equal("already faulted", skipped.Message);

        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingQueuedBatchProcessingResult.Succeeded(sequence, (RadarProcessingResult)null!));
        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingQueuedBatchProcessingResult.FailedProcessing(sequence, null!));
    }

    [Fact]
    public void QueuedSessionResultCopiesResultCollectionsAndKeepsIntakeSeparateFromProcessing()
    {
        var queued = new RadarProcessingQueuedBatch(
            RadarProcessingQueuedBatchSequence.Initial,
            CreateOwnedBatch());
        var enqueueResults = new List<RadarProcessingQueuedBatchEnqueueResult>
        {
            RadarProcessingQueuedBatchEnqueueResult.Accepted(queued),
            RadarProcessingQueuedBatchEnqueueResult.Full()
        };
        var processingResults = new List<RadarProcessingQueuedBatchProcessingResult>
        {
            RadarProcessingQueuedBatchProcessingResult.Succeeded(
                RadarProcessingQueuedBatchSequence.Initial,
                CreateProcessingResult())
        };
        var telemetry = new RadarProcessingProviderQueueTelemetrySummary(
            enqueueAttemptCount: 2,
            enqueuedBatchCount: 1,
            enqueueFullCount: 1,
            dequeuedBatchCount: 1,
            completedBatchCount: 1);

        var result = new RadarProcessingQueuedSessionResult(
            RadarProcessingQueuedSessionStatus.Completed,
            telemetry,
            enqueueResults,
            processingResults,
            "complete");
        enqueueResults.Clear();
        processingResults.Clear();

        Assert.True(result.IsCompleted);
        Assert.False(result.IsFaulted);
        Assert.False(result.IsCanceled);
        Assert.Same(telemetry, result.Telemetry);
        Assert.Equal(2, result.EnqueueResults.Count);
        Assert.Single(result.ProcessingResults);
        Assert.Equal("complete", result.Message);
        Assert.True(result.EnqueueResults[0].IsAccepted);
        Assert.False(result.EnqueueResults[1].IsAccepted);
        Assert.True(result.ProcessingResults[0].IsSuccessful);

        var faulted = new RadarProcessingQueuedSessionResult(RadarProcessingQueuedSessionStatus.Faulted);
        var canceled = new RadarProcessingQueuedSessionResult(RadarProcessingQueuedSessionStatus.Canceled);

        Assert.True(faulted.IsFaulted);
        Assert.True(canceled.IsCanceled);
        Assert.Same(RadarProcessingProviderQueueTelemetrySummary.Empty, faulted.Telemetry);
        Assert.Empty(faulted.EnqueueResults);
        Assert.Empty(faulted.ProcessingResults);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQueuedSessionResult((RadarProcessingQueuedSessionStatus)255));
        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingQueuedSessionResult(
                RadarProcessingQueuedSessionStatus.Completed,
                enqueueResults: new RadarProcessingQueuedBatchEnqueueResult[] { null! }));
        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingQueuedSessionResult(
                RadarProcessingQueuedSessionStatus.Completed,
                processingResults: new RadarProcessingQueuedBatchProcessingResult[] { null! }));
    }
}
