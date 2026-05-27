namespace RadarPulse.Domain.Processing;

/// <summary>
/// Applies quarantine lifecycle policy to partition evidence.
/// </summary>
/// <remarks>
/// The evaluator emits transitions when a partition enters quarantine, becomes
/// retry-eligible, clears quarantine, or reenters quarantine. Otherwise it returns
/// the updated state without a transition.
/// </remarks>
public sealed class RadarProcessingQuarantineLifecycleEvaluator
{
    /// <summary>
    /// Creates a lifecycle evaluator.
    /// </summary>
    public RadarProcessingQuarantineLifecycleEvaluator(
        RadarProcessingQuarantineLifecycleOptions? options = null)
    {
        Options = options ?? RadarProcessingQuarantineLifecycleOptions.Default;
    }

    /// <summary>
    /// Policy options used by the evaluator.
    /// </summary>
    public RadarProcessingQuarantineLifecycleOptions Options { get; }

    /// <summary>
    /// Evaluates evidence against the current lifecycle state.
    /// </summary>
    public RadarProcessingQuarantineLifecycleEvaluationResult Evaluate(
        RadarProcessingQuarantineLifecycleState state,
        RadarProcessingQuarantineEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(evidence);

        if (state.IsRetryEligible)
        {
            return EvaluateRetryEligible(state, evidence);
        }

        if (state.IsQuarantined)
        {
            return EvaluateActiveQuarantine(state, evidence);
        }

        return evidence.ObservedClassification == RadarProcessingHotPartitionClassification.Quarantined
            ? EnterQuarantine(state, evidence)
            : RecordCurrentClassification(state, evidence);
    }

    private RadarProcessingQuarantineLifecycleEvaluationResult EvaluateRetryEligible(
        RadarProcessingQuarantineLifecycleState state,
        RadarProcessingQuarantineEvidence evidence)
    {
        if (evidence.ObservedClassification == RadarProcessingHotPartitionClassification.Quarantined)
        {
            var reentered = state.ReenterQuarantine(evidence);
            return WithTransition(
                state,
                reentered,
                RadarProcessingQuarantineTransitionReason.ReenteredQuarantine,
                evidence);
        }

        var cleared = state.ClearToClassification(
            evidence,
            MapObservedClassification(evidence.ObservedClassification),
            RadarProcessingQuarantineTransitionReason.ClearedByEffectiveRelief);
        return WithTransition(
            state,
            cleared,
            RadarProcessingQuarantineTransitionReason.ClearedByEffectiveRelief,
            evidence);
    }

    private RadarProcessingQuarantineLifecycleEvaluationResult EvaluateActiveQuarantine(
        RadarProcessingQuarantineLifecycleState state,
        RadarProcessingQuarantineEvidence evidence)
    {
        var sampled = IsCoolingSample(evidence.PartitionBand)
            ? state.RecordCoolingSample(evidence)
            : state.RecordHotSample(evidence);

        if (sampled.SustainedCoolingSampleCount >= Options.SustainedCoolingSampleCount)
        {
            var cleared = sampled.Clear(
                evidence,
                RadarProcessingQuarantineTransitionReason.ClearedBySustainedCooling);
            return WithTransition(
                sampled,
                cleared,
                RadarProcessingQuarantineTransitionReason.ClearedBySustainedCooling,
                evidence);
        }

        if (sampled.QuarantineAgeEvaluations >= Options.QuarantineTtlEvaluations)
        {
            var retryEligible = sampled.MarkRetryEligible(
                evidence,
                RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl);
            return WithTransition(
                sampled,
                retryEligible,
                RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl,
                evidence);
        }

        if (HasMaterialPressureChange(sampled))
        {
            var retryEligible = sampled.MarkRetryEligible(
                evidence,
                RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByPressureChange);
            return WithTransition(
                sampled,
                retryEligible,
                RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByPressureChange,
                evidence);
        }

        return new RadarProcessingQuarantineLifecycleEvaluationResult(sampled);
    }

    private RadarProcessingQuarantineLifecycleEvaluationResult EnterQuarantine(
        RadarProcessingQuarantineLifecycleState state,
        RadarProcessingQuarantineEvidence evidence)
    {
        var quarantined = state.EnterQuarantine(evidence);
        return WithTransition(
            state,
            quarantined,
            RadarProcessingQuarantineTransitionReason.EnteredQuarantine,
            evidence);
    }

    private static RadarProcessingQuarantineLifecycleEvaluationResult RecordCurrentClassification(
        RadarProcessingQuarantineLifecycleState state,
        RadarProcessingQuarantineEvidence evidence) =>
        new(
            state.RecordClassificationEvidence(
                evidence,
                MapObservedClassification(evidence.ObservedClassification)));

    private bool HasMaterialPressureChange(
        RadarProcessingQuarantineLifecycleState state)
    {
        if (state.BaselinePressure is not { } baselinePressure)
        {
            return false;
        }

        var denominator = Math.Max(baselinePressure.Value, 1.0);
        var relativeChange = Math.Abs(state.LatestPressure.Value - baselinePressure.Value) / denominator;
        return relativeChange >= Options.MaterialPressureChangeThreshold;
    }

    private static bool IsCoolingSample(
        RadarProcessingPressureBand band) =>
        band is RadarProcessingPressureBand.Cold or RadarProcessingPressureBand.Normal;

    private static RadarProcessingQuarantineEffectiveClassification MapObservedClassification(
        RadarProcessingHotPartitionClassification classification) =>
        classification switch
        {
            RadarProcessingHotPartitionClassification.None =>
                RadarProcessingQuarantineEffectiveClassification.None,
            RadarProcessingHotPartitionClassification.MovableHot =>
                RadarProcessingQuarantineEffectiveClassification.MovableHot,
            RadarProcessingHotPartitionClassification.IntrinsicHot =>
                RadarProcessingQuarantineEffectiveClassification.IntrinsicHot,
            RadarProcessingHotPartitionClassification.Quarantined =>
                throw new ArgumentOutOfRangeException(nameof(classification), classification, "Quarantined evidence requires a transition."),
            _ => throw new ArgumentOutOfRangeException(nameof(classification), classification, "Classification is not defined.")
        };

    private static RadarProcessingQuarantineLifecycleEvaluationResult WithTransition(
        RadarProcessingQuarantineLifecycleState previous,
        RadarProcessingQuarantineLifecycleState current,
        RadarProcessingQuarantineTransitionReason reason,
        RadarProcessingQuarantineEvidence evidence) =>
        new(
            current,
            new RadarProcessingQuarantineTransition(
                evidence.PartitionId,
                evidence.ShardId,
                evidence.EvaluationSequence,
                evidence.TopologyVersion,
                previous.EffectiveClassification,
                current.EffectiveClassification,
                reason,
                evidence.PartitionPressure,
                CalculateTransitionAge(previous, current, reason, evidence)));

    private static long CalculateTransitionAge(
        RadarProcessingQuarantineLifecycleState previous,
        RadarProcessingQuarantineLifecycleState current,
        RadarProcessingQuarantineTransitionReason reason,
        RadarProcessingQuarantineEvidence evidence)
    {
        if (reason is RadarProcessingQuarantineTransitionReason.EnteredQuarantine or
            RadarProcessingQuarantineTransitionReason.ReenteredQuarantine)
        {
            return current.QuarantineAgeEvaluations;
        }

        return previous.QuarantineStartSequence is long previousStartSequence
            ? evidence.EvaluationSequence - previousStartSequence
            : current.QuarantineAgeEvaluations;
    }
}
