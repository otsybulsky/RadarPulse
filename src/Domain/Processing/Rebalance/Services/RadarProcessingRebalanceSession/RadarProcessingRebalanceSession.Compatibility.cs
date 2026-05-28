namespace RadarPulse.Domain.Processing;

public sealed partial class RadarProcessingRebalanceSession
{
    private void EnsureCompatibleProcessingResult(
        RadarProcessingResult processingResult)
    {
        if (processingResult.ExecutionMode != core.Options.ExecutionMode)
        {
            throw new ArgumentException(
                "Processing result execution mode must match the rebalance session core.",
                nameof(processingResult));
        }

        if (processingResult.PartitionCount != core.Options.PartitionCount ||
            processingResult.ShardCount != core.Options.ShardCount)
        {
            throw new ArgumentException(
                "Processing result topology shape must match the rebalance session core.",
                nameof(processingResult));
        }

        if (processingResult.TopologyVersion != core.Topology.Version)
        {
            throw new ArgumentException(
                "Processing result topology version must match the current rebalance session topology.",
                nameof(processingResult));
        }
    }

    private void EnsureCompatibleShape(
        RadarProcessingRebalancePolicyState candidatePolicyState,
        RadarProcessingHotPartitionClassifier candidateHotPartitionClassifier,
        RadarProcessingQuarantineLifecycleTracker candidateQuarantineLifecycleTracker)
    {
        if (candidatePolicyState.PartitionCount != core.Options.PartitionCount)
        {
            throw new ArgumentException(
                "Rebalance policy partition count must match the processing core.",
                nameof(policyState));
        }

        if (candidatePolicyState.ShardCount != core.Options.ShardCount)
        {
            throw new ArgumentException(
                "Rebalance policy shard count must match the processing core.",
                nameof(policyState));
        }

        if (candidateHotPartitionClassifier.PartitionCount != core.Options.PartitionCount)
        {
            throw new ArgumentException(
                "Hot partition classifier partition count must match the processing core.",
                nameof(hotPartitionClassifier));
        }

        if (candidateQuarantineLifecycleTracker.PartitionCount != core.Options.PartitionCount)
        {
            throw new ArgumentException(
                "Quarantine lifecycle partition count must match the processing core.",
                nameof(quarantineLifecycleTracker));
        }
    }
}
