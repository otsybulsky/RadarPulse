using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingQueuedProviderValidatorTests
{
    [Fact]
    public void QueuedProviderValidationContractsUseStableValues()
    {
        Assert.Equal(0, (int)RadarProcessingQueuedProviderValidationProfile.Off);
        Assert.Equal(1, (int)RadarProcessingQueuedProviderValidationProfile.Essential);
        Assert.Equal(2, (int)RadarProcessingQueuedProviderValidationProfile.Diagnostic);
        Assert.Equal(3, (int)RadarProcessingQueuedProviderValidationProfile.Benchmark);

        Assert.Equal(0, (int)RadarProcessingQueuedProviderValidationError.None);
        Assert.Equal(1, (int)RadarProcessingQueuedProviderValidationError.NonOwnedQueuedBatch);
        Assert.Equal(2, (int)RadarProcessingQueuedProviderValidationError.ProviderSequenceRegression);
        Assert.Equal(3, (int)RadarProcessingQueuedProviderValidationError.ProcessingSequenceRegression);
        Assert.Equal(4, (int)RadarProcessingQueuedProviderValidationError.MissingCompletionForAcceptedBatch);
        Assert.Equal(5, (int)RadarProcessingQueuedProviderValidationError.TopologyVersionRegression);
        Assert.Equal(9, (int)RadarProcessingQueuedProviderValidationError.DeterministicChecksumMismatch);

        var valid = RadarProcessingQueuedProviderValidationResult.Valid(
            RadarProcessingQueuedProviderValidationProfile.Diagnostic);
        var invalid = RadarProcessingQueuedProviderValidationResult.Invalid(
            RadarProcessingQueuedProviderValidationError.FailureCountMismatch,
            "failed batch count mismatch",
            RadarProcessingQueuedProviderValidationProfile.Benchmark,
            expectedCount: 1,
            actualCount: 2);

        Assert.True(valid.IsValid);
        Assert.Equal(RadarProcessingQueuedProviderValidationError.None, valid.Error);
        Assert.False(invalid.IsValid);
        Assert.Equal(RadarProcessingQueuedProviderValidationError.FailureCountMismatch, invalid.Error);
        Assert.Equal(1, invalid.ExpectedCount);
        Assert.Equal(2, invalid.ActualCount);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingQueuedProviderValidationResult.Valid((RadarProcessingQueuedProviderValidationProfile)255));
        Assert.Throws<ArgumentException>(() =>
            RadarProcessingQueuedProviderValidationResult.Invalid(
                RadarProcessingQueuedProviderValidationError.None,
                "invalid",
                RadarProcessingQueuedProviderValidationProfile.Diagnostic));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQueuedProviderReference(failedBatchCount: -1));
    }

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

    [Fact]
    public void BenchmarkReferenceComparisonCatchesChecksumMismatch()
    {
        var session = CreateValidCompletedSession(checksum: 10);
        var reference = new RadarProcessingQueuedProviderReference(validationChecksum: 11);

        var result = RadarProcessingQueuedProviderValidator.ValidateSessionResult(
            session,
            RadarProcessingQueuedProviderValidationProfile.Benchmark,
            reference);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingQueuedProviderValidationError.DeterministicChecksumMismatch, result.Error);
        Assert.Equal(11UL, result.ExpectedChecksum);
        Assert.Equal(10UL, result.ActualChecksum);
    }

    [Fact]
    public void BenchmarkReferenceComparisonCatchesAcceptedMoveMismatch()
    {
        var session = CreateValidCompletedSession(checksum: 10);
        var reference = new RadarProcessingQueuedProviderReference(
            validationChecksum: 10,
            acceptedMoveCount: 1);

        var result = RadarProcessingQueuedProviderValidator.ValidateSessionResult(
            session,
            RadarProcessingQueuedProviderValidationProfile.Benchmark,
            reference);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingQueuedProviderValidationError.AcceptedMoveCountMismatch, result.Error);
        Assert.Equal(1, result.ExpectedCount);
        Assert.Equal(0, result.ActualCount);
    }

    [Fact]
    public void BenchmarkReferenceComparisonAcceptsMatchingStructuralSession()
    {
        var session = CreateValidCompletedSession(checksum: 10);
        var reference = RadarProcessingQueuedProviderReference.FromQueuedSession(session);

        var result = RadarProcessingQueuedProviderValidator.ValidateSessionResult(
            session,
            RadarProcessingQueuedProviderValidationProfile.Benchmark,
            reference);

        Assert.True(result.IsValid, result.Message);
    }

    private static RadarProcessingQueuedSessionResult CreateValidCompletedSession(ulong checksum) =>
        CreateSessionResult(
            [CreateAccepted(0)],
            [
                RadarProcessingQueuedBatchProcessingResult.Succeeded(
                    RadarProcessingQueuedBatchSequence.Initial,
                    CreateProcessingResult(checksum: checksum))
            ],
            completedCount: 1);

    private static RadarProcessingQueuedSessionResult CreateSessionResult(
        IReadOnlyCollection<RadarProcessingQueuedBatchEnqueueResult> enqueueResults,
        IReadOnlyCollection<RadarProcessingQueuedBatchProcessingResult> processingResults,
        long completedCount,
        RadarProcessingQueuedSessionStatus status = RadarProcessingQueuedSessionStatus.Completed,
        RadarProcessingTopologyVersion? finalTopologyVersion = null)
    {
        var accepted = enqueueResults.LongCount(static result => result.IsAccepted);
        var failed = processingResults.LongCount(static result =>
            result.Status is RadarProcessingQueuedBatchProcessingStatus.FailedProcessing or
                RadarProcessingQueuedBatchProcessingStatus.FailedValidation or
                RadarProcessingQueuedBatchProcessingStatus.FailedMigration);
        var canceled = processingResults.LongCount(static result =>
            result.Status == RadarProcessingQueuedBatchProcessingStatus.Canceled);
        var skipped = processingResults.LongCount(static result =>
            result.Status == RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault);
        var telemetry = new RadarProcessingProviderQueueTelemetrySummary(
            enqueueAttemptCount: enqueueResults.Count,
            enqueuedBatchCount: accepted,
            dequeuedBatchCount: processingResults.Count,
            completedBatchCount: completedCount,
            failedBatchCount: failed,
            canceledBatchCount: canceled,
            skippedAfterFaultCount: skipped);

        return new RadarProcessingQueuedSessionResult(
            status,
            telemetry,
            enqueueResults,
            processingResults,
            finalTopologyVersion: finalTopologyVersion ?? RadarProcessingTopologyVersion.Initial);
    }

    private static RadarProcessingQueuedBatchEnqueueResult CreateAccepted(long sequence) =>
        RadarProcessingQueuedBatchEnqueueResult.Accepted(
            new RadarProcessingQueuedBatch(
                new RadarProcessingQueuedBatchSequence(sequence),
                CreateOwnedBatch((byte)(sequence + 1))));

    private static RadarEventBatch CreateOwnedBatch(byte firstPayloadValue)
    {
        var builder = CreateSingleEventBuilder(firstPayloadValue);
        return builder.Build();
    }

    private static RadarEventBatchBuilder CreateSingleEventBuilder(byte firstPayloadValue = 1)
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: 2);
        builder.AddEvent(
            new RadarStreamIdentity(
                sourceId: 0,
                radarOrdinal: 0,
                momentId: 0,
                elevationSlot: 0,
                azimuthBucket: 0,
                rangeBand: 0,
                dictionaryVersion: DictionaryVersion.Initial,
                sourceUniverseVersion: SourceUniverseVersion.Initial),
            volumeTimestampUtcTicks: 1,
            messageTimestampUtcTicks: 2,
            sourceRecord: 0,
            sourceMessage: 0,
            radialSequence: 0,
            gateStart: 0,
            gateCount: 2,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: [firstPayloadValue, (byte)(firstPayloadValue + 1)]);
        return builder;
    }

    private static RadarProcessingResult CreateProcessingResult(
        ulong checksum = 10,
        RadarProcessingTopologyVersion? topologyVersion = null,
        RadarProcessingWorkerTelemetrySummary? workerTelemetry = null)
    {
        var metrics = new RadarProcessingMetrics(
            ProcessedBatchCount: 1,
            ProcessedStreamEventCount: 1,
            ProcessedPayloadValueCount: 2,
            ActiveSourceCount: 1,
            RawValueChecksum: 3,
            ProcessingChecksum: checksum);

        return new RadarProcessingResult(
            RadarProcessingExecutionMode.Sequential,
            partitionCount: 1,
            shardCount: 1,
            metrics,
            RadarProcessingValidationResult.Valid(metrics),
            topologyVersion: topologyVersion,
            workerTelemetry: workerTelemetry);
    }

    private static RadarProcessingWorkerTelemetrySummary CreateFailedWorkerTelemetry() =>
        new(
            new RadarProcessingWorkerTelemetryCounters(
                dispatchedBatchCount: 1,
                completedBatchCount: 1,
                failedBatchCount: 1),
            workerCount: 1,
            queueCapacity: 1,
            Array.Empty<RadarProcessingRecentWorkerBatch>(),
            Array.Empty<RadarProcessingRecentWorkerFailure>(),
            new RadarProcessingWorkerRetentionStats());
}
