using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedProviderValidatorTests
{
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
    public void BenchmarkReferenceComparisonCatchesPayloadValueCountMismatch()
    {
        var session = CreateValidCompletedSession(checksum: 10);
        var reference = new RadarProcessingQueuedProviderReference(
            validationChecksum: 10,
            payloadValueCount: 3);

        var result = RadarProcessingQueuedProviderValidator.ValidateSessionResult(
            session,
            RadarProcessingQueuedProviderValidationProfile.Benchmark,
            reference);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingQueuedProviderValidationError.PayloadValueCountMismatch, result.Error);
        Assert.Equal(3, result.ExpectedCount);
        Assert.Equal(2, result.ActualCount);
    }

    [Fact]
    public void BenchmarkReferenceComparisonCatchesFailedMigrationMismatch()
    {
        var session = CreateSessionResult(
            [CreateAccepted(0)],
            [
                RadarProcessingQueuedBatchProcessingResult.FailedMigration(
                    RadarProcessingQueuedBatchSequence.Initial,
                    "migration failed")
            ],
            completedCount: 0,
            status: RadarProcessingQueuedSessionStatus.Faulted);
        var reference = new RadarProcessingQueuedProviderReference(
            failedBatchCount: 1,
            failedMigrationCount: 0);

        var result = RadarProcessingQueuedProviderValidator.ValidateSessionResult(
            session,
            RadarProcessingQueuedProviderValidationProfile.Benchmark,
            reference);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingQueuedProviderValidationError.FailedMigrationCountMismatch, result.Error);
        Assert.Equal(0, result.ExpectedCount);
        Assert.Equal(1, result.ActualCount);
    }

}
