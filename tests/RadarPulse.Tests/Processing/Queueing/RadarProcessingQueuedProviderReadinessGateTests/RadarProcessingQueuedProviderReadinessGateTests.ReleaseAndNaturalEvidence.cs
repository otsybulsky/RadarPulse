using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedProviderReadinessGateTests
{
    [Fact]
    public void ReleaseHealthFailsRetentionFailureBeforeCleanupCanPass()
    {
        var release = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateRetainedResourceReleaseHealth(
            new RadarProcessingRetainedPayloadTelemetrySummary(
                RadarProcessingRetainedPayloadStrategy.PooledCopy,
                retentionAttemptCount: 2,
                retainedBatchCount: 1,
                retentionFailedCopyCount: 1,
                releaseAttemptCount: 1,
                releasedBatchCount: 1));

        Assert.True(release.IsFailed);
        Assert.Equal(RadarProcessingQueuedProviderReadinessGate.RetainedResourceReleaseHealth, release.Gate);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.RetainedResourceRetentionFailed, release.Error);
        Assert.Equal(0, release.ExpectedCount);
        Assert.Equal(1, release.ActualCount);
    }

    [Fact]
    public void NaturalEvidenceRejectsControlledDelayAndNonCandidateContours()
    {
        var controlled = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateNaturalEvidence(
            isDefaultCandidateContour: true,
            overlapConsumerDelay: TimeSpan.FromMilliseconds(1));
        var nonCandidate = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateNaturalEvidence(
            isDefaultCandidateContour: false,
            overlapConsumerDelay: TimeSpan.Zero);
        var naturalCandidate = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateNaturalEvidence(
            isDefaultCandidateContour: true,
            overlapConsumerDelay: TimeSpan.Zero);

        Assert.True(controlled.IsFailed);
        Assert.Equal(RadarProcessingQueuedProviderReadinessGate.NaturalEvidence, controlled.Gate);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.ControlledProofExcluded, controlled.Error);
        Assert.True(nonCandidate.IsInconclusive);
        Assert.Equal(RadarProcessingQueuedProviderReadinessGate.EffectiveConfiguration, nonCandidate.Gate);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.CandidateContourMismatch, nonCandidate.Error);
        Assert.True(naturalCandidate.IsPassed);
    }
}
