namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingQuarantineLifecycleState
{
    private RadarProcessingQuarantineLifecycleState(
        int partitionId,
        int shardId,
        RadarProcessingQuarantineEffectiveClassification effectiveClassification,
        RadarProcessingTopologyVersion latestTopologyVersion,
        long latestEvidenceSequence,
        long? quarantineStartSequence,
        RadarProcessingPressureScore? baselinePressure,
        RadarProcessingPressureScore latestPressure,
        RadarProcessingPressureBand latestPressureBand,
        int sustainedCoolingSampleCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(partitionId);
        ArgumentOutOfRangeException.ThrowIfNegative(shardId);
        ArgumentOutOfRangeException.ThrowIfNegative(latestEvidenceSequence);
        ArgumentOutOfRangeException.ThrowIfNegative(sustainedCoolingSampleCount);
        RadarProcessingQuarantineTransition.EnsureKnownClassification(
            effectiveClassification,
            nameof(effectiveClassification));

        if (!Enum.IsDefined(latestPressureBand))
        {
            throw new ArgumentOutOfRangeException(nameof(latestPressureBand), latestPressureBand, "Pressure band is not defined.");
        }

        if (quarantineStartSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quarantineStartSequence));
        }

        if (quarantineStartSequence is null && baselinePressure is not null)
        {
            throw new ArgumentException("Baseline pressure requires quarantine evidence.", nameof(baselinePressure));
        }

        if (quarantineStartSequence is long startSequence &&
            startSequence > latestEvidenceSequence)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quarantineStartSequence),
                quarantineStartSequence,
                "Quarantine start sequence must not be later than latest evidence sequence.");
        }

        PartitionId = partitionId;
        ShardId = shardId;
        EffectiveClassification = effectiveClassification;
        LatestTopologyVersion = latestTopologyVersion;
        LatestEvidenceSequence = latestEvidenceSequence;
        QuarantineStartSequence = quarantineStartSequence;
        BaselinePressure = baselinePressure;
        LatestPressure = latestPressure;
        LatestPressureBand = latestPressureBand;
        SustainedCoolingSampleCount = sustainedCoolingSampleCount;
    }

    public int PartitionId { get; }

    public int ShardId { get; }

    public RadarProcessingQuarantineEffectiveClassification EffectiveClassification { get; }

    public RadarProcessingTopologyVersion LatestTopologyVersion { get; }

    public long LatestEvidenceSequence { get; }

    public long? QuarantineStartSequence { get; }

    public RadarProcessingPressureScore? BaselinePressure { get; }

    public RadarProcessingPressureScore LatestPressure { get; }

    public RadarProcessingPressureBand LatestPressureBand { get; }

    public int SustainedCoolingSampleCount { get; }

    public bool HasQuarantineEvidence => QuarantineStartSequence is not null;

    public bool IsRetryEligible => EffectiveClassification == RadarProcessingQuarantineEffectiveClassification.RetryEligible;

    public bool IsQuarantined => EffectiveClassification == RadarProcessingQuarantineEffectiveClassification.Quarantined;

    public bool BlocksDirectMove =>
        EffectiveClassification is
            RadarProcessingQuarantineEffectiveClassification.IntrinsicHot or
            RadarProcessingQuarantineEffectiveClassification.Quarantined;

    public long QuarantineAgeEvaluations =>
        QuarantineStartSequence is long start
            ? LatestEvidenceSequence - start
            : 0;

    public static RadarProcessingQuarantineLifecycleState Unclassified(
        int partitionId) =>
        new(
            partitionId,
            shardId: 0,
            RadarProcessingQuarantineEffectiveClassification.None,
            RadarProcessingTopologyVersion.Initial,
            latestEvidenceSequence: 0,
            quarantineStartSequence: null,
            baselinePressure: null,
            latestPressure: default,
            RadarProcessingPressureBand.Cold,
            sustainedCoolingSampleCount: 0);

    public RadarProcessingQuarantineLifecycleState EnterQuarantine(
        RadarProcessingQuarantineEvidence evidence)
    {
        EnsureMatchingEvidence(evidence);

        return FromEvidence(
            evidence,
            RadarProcessingQuarantineEffectiveClassification.Quarantined,
            quarantineStartSequence: evidence.EvaluationSequence,
            baselinePressure: evidence.PartitionPressure,
            sustainedCoolingSampleCount: 0);
    }

    public RadarProcessingQuarantineLifecycleState RecordCoolingSample(
        RadarProcessingQuarantineEvidence evidence)
    {
        EnsureMatchingEvidence(evidence);

        return FromEvidence(
            evidence,
            EffectiveClassification,
            QuarantineStartSequence,
            BaselinePressure,
            checked(SustainedCoolingSampleCount + 1));
    }

    public RadarProcessingQuarantineLifecycleState RecordHotSample(
        RadarProcessingQuarantineEvidence evidence)
    {
        EnsureMatchingEvidence(evidence);

        return FromEvidence(
            evidence,
            EffectiveClassification,
            QuarantineStartSequence,
            BaselinePressure,
            sustainedCoolingSampleCount: 0);
    }

    public RadarProcessingQuarantineLifecycleState MarkRetryEligible(
        RadarProcessingQuarantineEvidence evidence,
        RadarProcessingQuarantineTransitionReason reason)
    {
        EnsureMatchingEvidence(evidence);
        EnsureRetryReason(reason);

        return FromEvidence(
            evidence,
            RadarProcessingQuarantineEffectiveClassification.RetryEligible,
            QuarantineStartSequence ?? evidence.EvaluationSequence,
            BaselinePressure ?? evidence.PartitionPressure,
            SustainedCoolingSampleCount);
    }

    public RadarProcessingQuarantineLifecycleState Clear(
        RadarProcessingQuarantineEvidence evidence,
        RadarProcessingQuarantineTransitionReason reason)
    {
        EnsureMatchingEvidence(evidence);
        EnsureClearReason(reason);

        return FromEvidence(
            evidence,
            RadarProcessingQuarantineEffectiveClassification.None,
            quarantineStartSequence: null,
            baselinePressure: null,
            sustainedCoolingSampleCount: 0);
    }

    public RadarProcessingQuarantineLifecycleState ReenterQuarantine(
        RadarProcessingQuarantineEvidence evidence)
    {
        EnsureMatchingEvidence(evidence);

        return FromEvidence(
            evidence,
            RadarProcessingQuarantineEffectiveClassification.Quarantined,
            quarantineStartSequence: evidence.EvaluationSequence,
            baselinePressure: evidence.PartitionPressure,
            sustainedCoolingSampleCount: 0);
    }

    private RadarProcessingQuarantineLifecycleState FromEvidence(
        RadarProcessingQuarantineEvidence evidence,
        RadarProcessingQuarantineEffectiveClassification effectiveClassification,
        long? quarantineStartSequence,
        RadarProcessingPressureScore? baselinePressure,
        int sustainedCoolingSampleCount) =>
        new(
            evidence.PartitionId,
            evidence.ShardId,
            effectiveClassification,
            evidence.TopologyVersion,
            evidence.EvaluationSequence,
            quarantineStartSequence,
            baselinePressure,
            evidence.PartitionPressure,
            evidence.PartitionBand,
            sustainedCoolingSampleCount);

    private void EnsureMatchingEvidence(
        RadarProcessingQuarantineEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        if (evidence.PartitionId != PartitionId)
        {
            throw new ArgumentException("Evidence partition id must match lifecycle state.", nameof(evidence));
        }

        if (evidence.EvaluationSequence < LatestEvidenceSequence)
        {
            throw new ArgumentOutOfRangeException(
                nameof(evidence),
                evidence.EvaluationSequence,
                "Evidence sequence must not move backwards.");
        }
    }

    private static void EnsureRetryReason(
        RadarProcessingQuarantineTransitionReason reason)
    {
        if (reason is not RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl and
            not RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleBySustainedCooling and
            not RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByPressureChange)
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Reason must describe retry eligibility.");
        }
    }

    private static void EnsureClearReason(
        RadarProcessingQuarantineTransitionReason reason)
    {
        if (reason is not RadarProcessingQuarantineTransitionReason.ClearedBySustainedCooling and
            not RadarProcessingQuarantineTransitionReason.ClearedByEffectiveRelief and
            not RadarProcessingQuarantineTransitionReason.ClearedExplicitly)
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Reason must describe quarantine clearing.");
        }
    }
}
