namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingQuarantineLifecycleEvaluationResult
{
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

    public RadarProcessingQuarantineLifecycleState State { get; }

    public RadarProcessingQuarantineTransition? Transition { get; }

    public bool HasTransition => Transition is not null;
}
