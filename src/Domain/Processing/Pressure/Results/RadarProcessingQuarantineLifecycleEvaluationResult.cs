namespace RadarPulse.Domain.Processing;

/// <summary>
/// Result of evaluating quarantine lifecycle evidence for a partition.
/// </summary>
public sealed record RadarProcessingQuarantineLifecycleEvaluationResult
{
    /// <summary>
    /// Creates a quarantine lifecycle evaluation result.
    /// </summary>
    public RadarProcessingQuarantineLifecycleEvaluationResult(
        RadarProcessingQuarantineLifecycleState state,
        RadarProcessingQuarantineTransition? transition = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (transition is not null)
        {
            if (transition.PartitionId != state.PartitionId)
            {
                throw new ArgumentException("Transition partition id must match lifecycle state.", nameof(transition));
            }

            if (transition.EvaluationSequence != state.LatestEvidenceSequence)
            {
                throw new ArgumentException("Transition sequence must match lifecycle state.", nameof(transition));
            }

            if (transition.TopologyVersion != state.LatestTopologyVersion)
            {
                throw new ArgumentException("Transition topology version must match lifecycle state.", nameof(transition));
            }

            if (transition.CurrentClassification != state.EffectiveClassification)
            {
                throw new ArgumentException("Transition current classification must match lifecycle state.", nameof(transition));
            }
        }

        State = state;
        Transition = transition;
    }

    /// <summary>
    /// Updated lifecycle state after evaluation.
    /// </summary>
    public RadarProcessingQuarantineLifecycleState State { get; }

    /// <summary>
    /// Transition emitted by the evaluation, when state classification changed.
    /// </summary>
    public RadarProcessingQuarantineTransition? Transition { get; }

    /// <summary>
    /// Indicates whether the evaluation emitted a transition.
    /// </summary>
    public bool HasTransition => Transition is not null;
}
