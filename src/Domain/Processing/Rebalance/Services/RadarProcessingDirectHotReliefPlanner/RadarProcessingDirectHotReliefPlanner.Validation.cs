namespace RadarPulse.Domain.Processing;

public sealed partial class RadarProcessingDirectHotReliefPlanner
{
    private static RadarProcessingRebalanceDecision NoAction(
        long decisionId,
        RadarProcessingPressureWindow pressureWindow,
        RadarProcessingRebalancePolicyState policyState,
        IReadOnlyCollection<RadarProcessingRebalanceSkippedReason> skippedReasons) =>
        RadarProcessingRebalanceDecision.NoAction(
            decisionId,
            policyState.EvaluationSequence,
            pressureWindow.LatestTopologyVersion,
            pressureWindow.SampleCount,
            skippedReasons);

    private static void EnsureCompatibleShape(
        RadarProcessingPressureWindow pressureWindow,
        RadarProcessingRebalancePolicyState policyState,
        RadarProcessingHotPartitionClassifier? hotPartitionClassifier,
        RadarProcessingQuarantineLifecycleTracker? quarantineLifecycleTracker)
    {
        if (pressureWindow.Partitions.Count != policyState.PartitionCount)
        {
            throw new ArgumentException(
                "Pressure window partition count must match rebalance policy state.",
                nameof(pressureWindow));
        }

        if (pressureWindow.Shards.Count != policyState.ShardCount)
        {
            throw new ArgumentException(
                "Pressure window shard count must match rebalance policy state.",
                nameof(pressureWindow));
        }

        if (hotPartitionClassifier is not null &&
            pressureWindow.Partitions.Count != hotPartitionClassifier.PartitionCount)
        {
            throw new ArgumentException(
                "Pressure window partition count must match hot partition classifier state.",
                nameof(pressureWindow));
        }

        if (quarantineLifecycleTracker is not null &&
            pressureWindow.Partitions.Count != quarantineLifecycleTracker.PartitionCount)
        {
            throw new ArgumentException(
                "Pressure window partition count must match quarantine lifecycle state.",
                nameof(pressureWindow));
        }
    }

    private static bool TrySkipClassifiedPartition(
        RadarProcessingHotPartitionClassifier? hotPartitionClassifier,
        RadarProcessingQuarantineLifecycleTracker? quarantineLifecycleTracker,
        int partitionId,
        List<RadarProcessingRebalanceSkippedReason> skippedClassificationReasons)
    {
        if (quarantineLifecycleTracker is not null)
        {
            return TrySkipLifecyclePartition(
                quarantineLifecycleTracker,
                partitionId,
                skippedClassificationReasons);
        }

        if (hotPartitionClassifier is null)
        {
            return false;
        }

        var state = hotPartitionClassifier.GetPartition(partitionId);
        if (!state.BlocksDirectMove)
        {
            return false;
        }

        var reason = state.Classification switch
        {
            RadarProcessingHotPartitionClassification.IntrinsicHot =>
                RadarProcessingRebalanceSkippedReason.PartitionClassifiedIntrinsicHot,
            RadarProcessingHotPartitionClassification.Quarantined =>
                RadarProcessingRebalanceSkippedReason.PartitionQuarantined,
            _ => throw new InvalidOperationException("Only blocked hot partition classifications can be skipped.")
        };

        if (!skippedClassificationReasons.Contains(reason))
        {
            skippedClassificationReasons.Add(reason);
        }

        return true;
    }

    private static bool TrySkipLifecyclePartition(
        RadarProcessingQuarantineLifecycleTracker quarantineLifecycleTracker,
        int partitionId,
        List<RadarProcessingRebalanceSkippedReason> skippedClassificationReasons)
    {
        var state = quarantineLifecycleTracker.GetPartition(partitionId);
        if (!state.BlocksDirectMove)
        {
            return false;
        }

        var reason = state.EffectiveClassification switch
        {
            RadarProcessingQuarantineEffectiveClassification.IntrinsicHot =>
                RadarProcessingRebalanceSkippedReason.PartitionClassifiedIntrinsicHot,
            RadarProcessingQuarantineEffectiveClassification.Quarantined =>
                RadarProcessingRebalanceSkippedReason.PartitionQuarantined,
            _ => throw new InvalidOperationException("Only blocked quarantine lifecycle classifications can be skipped.")
        };

        if (!skippedClassificationReasons.Contains(reason))
        {
            skippedClassificationReasons.Add(reason);
        }

        return true;
    }
}
