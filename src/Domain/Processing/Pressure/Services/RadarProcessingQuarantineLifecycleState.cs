namespace RadarPulse.Domain.Processing;

/// <summary>
/// Immutable lifecycle state for one partition under quarantine policy.
/// </summary>
/// <remarks>
/// The state preserves the latest evidence sequence, topology version, baseline
/// quarantine pressure, sustained cooling count, and current effective
/// classification. State transitions are produced by creating a new instance.
/// </remarks>
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

    /// <summary>
    /// Partition represented by the lifecycle state.
    /// </summary>
    public int PartitionId { get; }

    /// <summary>
    /// Latest shard associated with the partition evidence.
    /// </summary>
    public int ShardId { get; }

    /// <summary>
    /// Effective classification after lifecycle policy is applied.
    /// </summary>
    public RadarProcessingQuarantineEffectiveClassification EffectiveClassification { get; }

    /// <summary>
    /// Latest topology version observed for the partition.
    /// </summary>
    public RadarProcessingTopologyVersion LatestTopologyVersion { get; }

    /// <summary>
    /// Latest evaluation sequence observed for the partition.
    /// </summary>
    public long LatestEvidenceSequence { get; }

    /// <summary>
    /// Evaluation sequence when current quarantine evidence started.
    /// </summary>
    public long? QuarantineStartSequence { get; }

    /// <summary>
    /// Pressure recorded when quarantine evidence started.
    /// </summary>
    public RadarProcessingPressureScore? BaselinePressure { get; }

    /// <summary>
    /// Latest observed partition pressure.
    /// </summary>
    public RadarProcessingPressureScore LatestPressure { get; }

    /// <summary>
    /// Latest observed partition pressure band.
    /// </summary>
    public RadarProcessingPressureBand LatestPressureBand { get; }

    /// <summary>
    /// Consecutive cooling samples observed while retaining quarantine evidence.
    /// </summary>
    public int SustainedCoolingSampleCount { get; }

    /// <summary>
    /// Indicates whether the state retains active quarantine evidence.
    /// </summary>
    public bool HasQuarantineEvidence => QuarantineStartSequence is not null;

    /// <summary>
    /// Indicates whether the partition can be retried after quarantine.
    /// </summary>
    public bool IsRetryEligible => EffectiveClassification == RadarProcessingQuarantineEffectiveClassification.RetryEligible;

    /// <summary>
    /// Indicates whether the partition is currently quarantined.
    /// </summary>
    public bool IsQuarantined => EffectiveClassification == RadarProcessingQuarantineEffectiveClassification.Quarantined;

    /// <summary>
    /// Indicates whether direct hot-relief should skip the partition.
    /// </summary>
    public bool BlocksDirectMove =>
        EffectiveClassification is
            RadarProcessingQuarantineEffectiveClassification.IntrinsicHot or
            RadarProcessingQuarantineEffectiveClassification.Quarantined;

    /// <summary>
    /// Number of evaluations since quarantine evidence started.
    /// </summary>
    public long QuarantineAgeEvaluations =>
        QuarantineStartSequence is long start
            ? LatestEvidenceSequence - start
            : 0;

    /// <summary>
    /// Creates an unclassified lifecycle state for a partition.
    /// </summary>
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

    /// <summary>
    /// Enters quarantine using the supplied evidence as the baseline.
    /// </summary>
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

    /// <summary>
    /// Records a cooling sample and increments sustained cooling count.
    /// </summary>
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

    /// <summary>
    /// Records a non-cooling sample and resets sustained cooling count.
    /// </summary>
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

    /// <summary>
    /// Records non-quarantine classification evidence.
    /// </summary>
    public RadarProcessingQuarantineLifecycleState RecordClassificationEvidence(
        RadarProcessingQuarantineEvidence evidence,
        RadarProcessingQuarantineEffectiveClassification effectiveClassification)
    {
        EnsureMatchingEvidence(evidence);
        EnsureNonQuarantineClassification(effectiveClassification, nameof(effectiveClassification));

        return FromEvidence(
            evidence,
            effectiveClassification,
            quarantineStartSequence: null,
            baselinePressure: null,
            sustainedCoolingSampleCount: 0);
    }

    /// <summary>
    /// Marks a quarantined partition as retry-eligible while preserving quarantine evidence.
    /// </summary>
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

    /// <summary>
    /// Clears quarantine evidence to an unclassified state.
    /// </summary>
    public RadarProcessingQuarantineLifecycleState Clear(
        RadarProcessingQuarantineEvidence evidence,
        RadarProcessingQuarantineTransitionReason reason) =>
        ClearToClassification(
            evidence,
            RadarProcessingQuarantineEffectiveClassification.None,
            reason);

    /// <summary>
    /// Clears quarantine evidence to a non-quarantine classification.
    /// </summary>
    public RadarProcessingQuarantineLifecycleState ClearToClassification(
        RadarProcessingQuarantineEvidence evidence,
        RadarProcessingQuarantineEffectiveClassification effectiveClassification,
        RadarProcessingQuarantineTransitionReason reason)
    {
        EnsureMatchingEvidence(evidence);
        EnsureNonQuarantineClassification(effectiveClassification, nameof(effectiveClassification));
        EnsureClearReason(reason);

        return FromEvidence(
            evidence,
            effectiveClassification,
            quarantineStartSequence: null,
            baselinePressure: null,
            sustainedCoolingSampleCount: 0);
    }

    /// <summary>
    /// Reenters quarantine after retry evidence still indicates quarantine.
    /// </summary>
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

    private static void EnsureNonQuarantineClassification(
        RadarProcessingQuarantineEffectiveClassification classification,
        string paramName)
    {
        RadarProcessingQuarantineTransition.EnsureKnownClassification(classification, paramName);

        if (classification is RadarProcessingQuarantineEffectiveClassification.Quarantined or
            RadarProcessingQuarantineEffectiveClassification.RetryEligible)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                classification,
                "Classification must not retain quarantine evidence.");
        }
    }
}
