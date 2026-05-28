using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedProviderValidatorTests
{
    [Fact]
    public void BenchmarkReferenceComparisonRejectsSemanticSurfaceMismatch()
    {
        var session = CreateValidCompletedSession(checksum: 10);
        var reference = new RadarProcessingQueuedProviderReference(
            validationChecksum: 10,
            payloadValueCount: 2,
            acceptedMoveCount: 0,
            skippedDecisionCount: 0,
            failedBatchCount: 0,
            failedMigrationCount: 0,
            workerFailedBatchCount: 0,
            finalTopologyVersion: RadarProcessingTopologyVersion.Initial,
            semanticSurface: RadarProcessingQueuedProviderValidationSurface.Rebalance);
        var context = CreateValidationContext(session);

        var result = RadarProcessingQueuedProviderValidator.ValidateSessionResult(
            session,
            RadarProcessingQueuedProviderValidationProfile.Benchmark,
            reference,
            context);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingQueuedProviderValidationError.ReferenceSemanticSurfaceMismatch, result.Error);
        Assert.Equal((int)RadarProcessingQueuedProviderValidationSurface.Rebalance, result.ExpectedCount);
        Assert.Equal((int)RadarProcessingQueuedProviderValidationSurface.ProcessingOnly, result.ActualCount);
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

}
