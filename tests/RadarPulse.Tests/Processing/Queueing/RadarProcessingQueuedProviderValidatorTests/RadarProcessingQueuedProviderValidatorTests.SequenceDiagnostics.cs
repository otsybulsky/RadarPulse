using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedProviderValidatorTests
{
    [Fact]
    public void DiagnosticProfileCatchesLeasedQueuedBatchInput()
    {
        var builder = CreateSingleEventBuilder();

        builder.ConsumeLeased(batch =>
        {
            var result = RadarProcessingQueuedProviderValidator.ValidateQueuedBatch(batch);

            Assert.False(result.IsValid);
            Assert.Equal(RadarProcessingQueuedProviderValidationError.NonOwnedQueuedBatch, result.Error);
        });
    }

    [Fact]
    public void DiagnosticProfileCatchesOutOfOrderProcessedSequence()
    {
        var session = CreateSessionResult(
            [CreateAccepted(0), CreateAccepted(1)],
            [
                RadarProcessingQueuedBatchProcessingResult.Succeeded(
                    new RadarProcessingQueuedBatchSequence(1),
                    CreateProcessingResult(topologyVersion: new RadarProcessingTopologyVersion(1))),
                RadarProcessingQueuedBatchProcessingResult.Succeeded(
                    new RadarProcessingQueuedBatchSequence(0),
                    CreateProcessingResult())
            ],
            completedCount: 2,
            finalTopologyVersion: new RadarProcessingTopologyVersion(1));

        var result = RadarProcessingQueuedProviderValidator.ValidateSessionResult(session);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingQueuedProviderValidationError.ProcessingSequenceRegression, result.Error);
    }

    [Fact]
    public void EssentialProfileCatchesMissingCompletionForAcceptedBatch()
    {
        var session = CreateSessionResult(
            [CreateAccepted(0)],
            [],
            completedCount: 0);

        var result = RadarProcessingQueuedProviderValidator.ValidateSessionResult(
            session,
            RadarProcessingQueuedProviderValidationProfile.Essential);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingQueuedProviderValidationError.MissingCompletionForAcceptedBatch, result.Error);
    }

    [Fact]
    public void EssentialProfileCatchesAcceptedProviderSequenceGap()
    {
        var session = CreateSessionResult(
            [CreateAccepted(0), CreateAccepted(2)],
            [
                RadarProcessingQueuedBatchProcessingResult.Succeeded(
                    RadarProcessingQueuedBatchSequence.Initial,
                    CreateProcessingResult())
            ],
            completedCount: 1);

        var result = RadarProcessingQueuedProviderValidator.ValidateSessionResult(
            session,
            RadarProcessingQueuedProviderValidationProfile.Essential);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingQueuedProviderValidationError.ProviderSequenceGap, result.Error);
        Assert.Equal(1, result.ExpectedCount);
        Assert.Equal(2, result.ActualCount);
    }

    [Fact]
    public void EssentialProfileCatchesProcessedProviderSequenceGap()
    {
        var session = CreateSessionResult(
            [CreateAccepted(0), CreateAccepted(1)],
            [
                RadarProcessingQueuedBatchProcessingResult.Succeeded(
                    RadarProcessingQueuedBatchSequence.Initial,
                    CreateProcessingResult()),
                RadarProcessingQueuedBatchProcessingResult.Succeeded(
                    new RadarProcessingQueuedBatchSequence(2),
                    CreateProcessingResult())
            ],
            completedCount: 2);

        var result = RadarProcessingQueuedProviderValidator.ValidateSessionResult(
            session,
            RadarProcessingQueuedProviderValidationProfile.Essential);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingQueuedProviderValidationError.ProcessingSequenceGap, result.Error);
        Assert.Equal(1, result.ExpectedCount);
        Assert.Equal(2, result.ActualCount);
    }

    [Fact]
    public void DiagnosticProfileCatchesTopologyRegression()
    {
        var session = CreateSessionResult(
            [CreateAccepted(0), CreateAccepted(1)],
            [
                RadarProcessingQueuedBatchProcessingResult.Succeeded(
                    new RadarProcessingQueuedBatchSequence(0),
                    CreateProcessingResult(topologyVersion: new RadarProcessingTopologyVersion(2))),
                RadarProcessingQueuedBatchProcessingResult.Succeeded(
                    new RadarProcessingQueuedBatchSequence(1),
                    CreateProcessingResult(topologyVersion: new RadarProcessingTopologyVersion(1)))
            ],
            completedCount: 2,
            finalTopologyVersion: new RadarProcessingTopologyVersion(2));

        var result = RadarProcessingQueuedProviderValidator.ValidateSessionResult(session);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingQueuedProviderValidationError.TopologyVersionRegression, result.Error);
    }

    [Fact]
    public void DiagnosticProfileCatchesTelemetryCounterMismatch()
    {
        var session = CreateSessionResult(
            [CreateAccepted(0)],
            [
                RadarProcessingQueuedBatchProcessingResult.Succeeded(
                    RadarProcessingQueuedBatchSequence.Initial,
                    CreateProcessingResult())
            ],
            completedCount: 0);

        var result = RadarProcessingQueuedProviderValidator.ValidateSessionResult(session);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingQueuedProviderValidationError.TelemetryCounterMismatch, result.Error);
    }

    [Fact]
    public void DiagnosticProfileCatchesWorkerFailureNotReflectedByBatchStatus()
    {
        var session = CreateSessionResult(
            [CreateAccepted(0)],
            [
                RadarProcessingQueuedBatchProcessingResult.Succeeded(
                    RadarProcessingQueuedBatchSequence.Initial,
                    CreateProcessingResult(workerTelemetry: CreateFailedWorkerTelemetry()))
            ],
            completedCount: 1);

        var result = RadarProcessingQueuedProviderValidator.ValidateSessionResult(session);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingQueuedProviderValidationError.WorkerFailureCountMismatch, result.Error);
    }

}
